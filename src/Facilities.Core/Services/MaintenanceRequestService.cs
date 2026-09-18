using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Facilities.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Core.Services;

/// <summary>
/// The single place request-lifecycle rules are enforced, regardless of which front
/// door (JSON API or Razor Pages UI) called in. Tenant scoping relies on the EF global
/// query filter (keyed off ICurrentUserContext) plus an explicit post-fetch assertion
/// here as a second, independent layer — see EnsureSameTenant.
/// </summary>
public class MaintenanceRequestService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public MaintenanceRequestService(IAppDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MaintenanceRequest> CreateAsync(Guid siteId, string description, decimal estimatedCost, CancellationToken ct = default)
    {
        var organisation = await _db.Organisations.FirstOrDefaultAsync(o => o.Id == _currentUser.OrganisationId, ct)
            ?? throw new NotFoundException("Organisation not found.");

        // Scoped by the global query filter to the caller's org, so a siteId belonging
        // to another organisation simply won't be found here.
        var siteExists = await _db.Sites.AnyAsync(s => s.Id == siteId, ct);
        if (!siteExists)
            throw new NotFoundException($"Site '{siteId}' was not found.");

        var request = MaintenanceRequest.Raise(
            _currentUser.OrganisationId, siteId, _currentUser.UserId, description, estimatedCost, organisation.CostThresholdAmount);

        _db.MaintenanceRequests.Add(request);

        var action = request.Status == RequestStatus.PendingApproval ? "Raised" : "AutoApproved";
        _db.AuditLogEntries.Add(AuditLogEntry.For(
            _currentUser.OrganisationId, nameof(MaintenanceRequest), request.Id, action, _currentUser.UserId, null, request.Status));

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<List<MaintenanceRequest>> ListAsync(CancellationToken ct = default)
    {
        IQueryable<MaintenanceRequest> query = _db.MaintenanceRequests.AsNoTracking();

        // Requesters see their own submissions; Approvers see everything in the org
        // (they need the full picture to make approval decisions).
        if (_currentUser.Role == UserRole.Requester)
            query = query.Where(r => r.RequestedByUserId == _currentUser.UserId);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public async Task<MaintenanceRequest> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var request = await _db.MaintenanceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Maintenance request '{id}' was not found.");
        EnsureSameTenant(request);
        return request;
    }

    public async Task<MaintenanceRequest> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        RequireApprover();
        var request = await GetTrackedAsync(id, ct);
        var organisation = await _db.Organisations.FirstAsync(o => o.Id == _currentUser.OrganisationId, ct);
        var fromStatus = request.Status;

        request.Approve(_currentUser.UserId, organisation.CostThresholdAmount);

        _db.AuditLogEntries.Add(AuditLogEntry.For(
            _currentUser.OrganisationId, nameof(MaintenanceRequest), request.Id, "Approved", _currentUser.UserId, fromStatus, request.Status));

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<MaintenanceRequest> RejectAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        RequireApprover();
        var request = await GetTrackedAsync(id, ct);
        var fromStatus = request.Status;

        request.Reject(_currentUser.UserId);

        _db.AuditLogEntries.Add(AuditLogEntry.For(
            _currentUser.OrganisationId, nameof(MaintenanceRequest), request.Id, "Rejected", _currentUser.UserId, fromStatus, request.Status, reason));

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<MaintenanceRequest> CompleteAsync(Guid id, decimal actualCost, CancellationToken ct = default)
    {
        var request = await GetTrackedAsync(id, ct);
        var fromStatus = request.Status;

        request.Complete(actualCost, _currentUser.UserId);

        var action = request.ExceedsApprovedThreshold ? "CompletedOverThreshold" : "Completed";
        _db.AuditLogEntries.Add(AuditLogEntry.For(
            _currentUser.OrganisationId, nameof(MaintenanceRequest), request.Id, action, _currentUser.UserId, fromStatus, request.Status));

        await _db.SaveChangesAsync(ct);
        return request;
    }

    private async Task<MaintenanceRequest> GetTrackedAsync(Guid id, CancellationToken ct)
    {
        var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Maintenance request '{id}' was not found.");
        EnsureSameTenant(request);
        return request;
    }

    private void EnsureSameTenant(MaintenanceRequest request)
    {
        // The EF global query filter already scopes the query above to the caller's
        // organisation, so this branch should be unreachable in practice. It's kept as
        // a cheap, explicit second layer in case a filter is ever bypassed
        // (e.g. .IgnoreQueryFilters() added elsewhere later). 404, not 403, so a
        // cross-tenant ID guess doesn't even confirm the row exists.
        if (request.OrganisationId != _currentUser.OrganisationId)
            throw new NotFoundException($"Maintenance request '{request.Id}' was not found.");
    }

    private void RequireApprover()
    {
        if (_currentUser.Role != UserRole.Approver)
            throw new ForbiddenException("Only an Approver can perform this action.");
    }
}

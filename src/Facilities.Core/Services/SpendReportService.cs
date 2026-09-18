using Facilities.Core.Abstractions;
using Facilities.Core.Enums;
using Facilities.Core.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Core.Services;

public class SpendReportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public SpendReportService(IAppDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Total actual spend per site for the caller's organisation, for requests
    /// completed within [from, to]. Only Completed requests count as "spend" — an
    /// estimate isn't money spent yet. See DECISIONS.md.
    /// </summary>
    public async Task<List<SiteSpendSummary>> GetSpendBySiteAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? siteId = null, CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("'from' must not be after 'to'.");

        var query = _db.MaintenanceRequests.AsNoTracking()
            .Where(r => r.OrganisationId == _currentUser.OrganisationId
                        && r.Status == RequestStatus.Completed
                        && r.CompletedAt >= from
                        && r.CompletedAt <= to);

        if (siteId is not null)
            query = query.Where(r => r.SiteId == siteId);

        // The Where/filtering above runs server-side; only the final grouping and
        // aggregation happen client-side (per-org completed-request volumes are small
        // — see DECISIONS.md on out-of-scope performance tuning). This also sidesteps
        // a real gap between providers: the SQLite provider used in tests can't
        // translate this Join+GroupBy shape that Postgres handles natively.
        var completedRequests = await query.Select(r => new { r.SiteId, r.ActualCost }).ToListAsync(ct);
        var siteNames = await _db.Sites.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return completedRequests
            .GroupBy(r => r.SiteId)
            .Select(g => new SiteSpendSummary(g.Key, siteNames.GetValueOrDefault(g.Key, "Unknown"), g.Sum(r => r.ActualCost!.Value), g.Count()))
            .OrderBy(s => s.SiteName)
            .ToList();
    }
}

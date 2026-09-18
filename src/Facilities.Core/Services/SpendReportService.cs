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
                        && r.CompletedAt <= to)
            .Join(_db.Sites.AsNoTracking(), r => r.SiteId, s => s.Id, (r, s) => new { r.SiteId, s.Name, r.ActualCost });

        if (siteId is not null)
            query = query.Where(x => x.SiteId == siteId);

        return await query
            .GroupBy(x => new { x.SiteId, x.Name })
            .Select(g => new SiteSpendSummary(g.Key.SiteId, g.Key.Name, g.Sum(x => x.ActualCost!.Value), g.Count()))
            .OrderBy(s => s.SiteName)
            .ToListAsync(ct);
    }
}

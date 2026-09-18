using Facilities.Api.Contracts;
using Facilities.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Api.Controllers;

/// <summary>
/// A plain read with no business rule attached to it — listing the caller's own
/// sites — so it queries IAppDbContext directly rather than through a Core service.
/// Anything with an actual rule (approval, thresholds, tenant checks beyond the
/// global filter) lives in a Core service instead; see MaintenanceRequestService.
/// </summary>
[Route("api/sites")]
public class SitesController : ApiControllerBase
{
    private readonly IAppDbContext _db;

    public SitesController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<SiteDto>>> List(CancellationToken ct)
    {
        var sites = await _db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(sites.Select(SiteDto.FromEntity));
    }
}

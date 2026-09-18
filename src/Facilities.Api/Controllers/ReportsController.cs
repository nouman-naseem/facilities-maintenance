using Facilities.Api.Contracts;
using Facilities.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facilities.Api.Controllers;

[Route("api/reports")]
[Authorize(Roles = "Approver")]
public class ReportsController : ApiControllerBase
{
    private readonly SpendReportService _spendReport;

    public ReportsController(SpendReportService spendReport)
    {
        _spendReport = spendReport;
    }

    [HttpGet("spend")]
    public async Task<ActionResult<List<SiteSpendDto>>> Spend(
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] Guid? siteId, CancellationToken ct)
    {
        var summaries = await _spendReport.GetSpendBySiteAsync(from, to, siteId, ct);
        return Ok(summaries.Select(SiteSpendDto.FromSummary));
    }
}

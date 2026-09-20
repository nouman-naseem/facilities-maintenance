using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Facilities.Core.Exceptions;
using Facilities.Core.Reporting;
using Facilities.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Api.Pages.Reports;

public class IndexModel : PageModel
{
    private readonly SpendReportService _spendReport;
    private readonly IAppDbContext _db;

    public IndexModel(SpendReportService spendReport, IAppDbContext db)
    {
        _spendReport = spendReport;
        _db = db;
    }

    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public Guid? SiteId { get; private set; }
    public List<Site> Sites { get; private set; } = new();
    public List<SiteSpendSummary> Results { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task OnGetAsync(DateTime? from, DateTime? to, Guid? siteId, CancellationToken ct)
    {
        Sites = await _db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        From = from ?? DateTime.UtcNow.AddMonths(-1).Date;
        To = to ?? DateTime.UtcNow.Date;
        SiteId = siteId;

        try
        {
            // The date picker posts a bare "yyyy-MM-dd" with no timezone, which model
            // binding gives us as DateTime.Kind=Unspecified. Constructing the
            // DateTimeOffset explicitly with a zero offset treats the picked date as a
            // UTC calendar boundary; letting the implicit DateTime->DateTimeOffset
            // conversion do it instead would interpret it as server-local time and
            // silently shift the range by the server's UTC offset.
            var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(From, DateTimeKind.Unspecified), TimeSpan.Zero);
            var toUtc = new DateTimeOffset(DateTime.SpecifyKind(To.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified), TimeSpan.Zero);
            Results = await _spendReport.GetSpendBySiteAsync(fromUtc, toUtc, SiteId, ct);
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }
    }
}

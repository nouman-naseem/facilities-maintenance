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
            // 'to' is a calendar date from the picker; include the whole day.
            Results = await _spendReport.GetSpendBySiteAsync(From, To.AddDays(1).AddTicks(-1), SiteId, ct);
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }
    }
}

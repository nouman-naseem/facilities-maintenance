using Facilities.Core.Reporting;

namespace Facilities.Api.Contracts;

public record SiteSpendDto(Guid SiteId, string SiteName, decimal TotalActualCost, int CompletedRequestCount)
{
    public static SiteSpendDto FromSummary(SiteSpendSummary s) => new(s.SiteId, s.SiteName, s.TotalActualCost, s.CompletedRequestCount);
}

public record SiteDto(Guid Id, string Name)
{
    public static SiteDto FromEntity(Facilities.Core.Entities.Site s) => new(s.Id, s.Name);
}

namespace Facilities.Core.Reporting;

public record SiteSpendSummary(Guid SiteId, string SiteName, decimal TotalActualCost, int CompletedRequestCount);

using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Facilities.Core.Services;
using Facilities.Tests.TestSupport;

namespace Facilities.Tests.Services;

public class SpendReportServiceTests
{
    [Fact]
    public async Task GetSpendBySiteAsync_SumsOnlyCompletedRequestsWithinRangeForCallersOrganisation()
    {
        using var sqlite = new SqliteTestDatabase();

        Guid orgId, otherOrgId, siteId, otherOrgSiteId, requesterId, otherOrgRequesterId;
        using (var seedDb = sqlite.CreateContext(new TestCurrentUserContext()))
        {
            var org = Organisation.Create("Org A", costThresholdAmount: 100_000m); // high, so everything auto-approves
            var otherOrg = Organisation.Create("Org B", costThresholdAmount: 100_000m);
            seedDb.Organisations.AddRange(org, otherOrg);

            var site = Site.Create(org.Id, "Site 1");
            var otherOrgSite = Site.Create(otherOrg.Id, "Site X");
            seedDb.Sites.AddRange(site, otherOrgSite);

            var requester = User.Create(org.Id, "requester@a.test", "hash", UserRole.Requester);
            var otherOrgRequester = User.Create(otherOrg.Id, "requester@b.test", "hash", UserRole.Requester);
            seedDb.Users.AddRange(requester, otherOrgRequester);

            await seedDb.SaveChangesAsync();
            orgId = org.Id;
            otherOrgId = otherOrg.Id;
            siteId = site.Id;
            otherOrgSiteId = otherOrgSite.Id;
            requesterId = requester.Id;
            otherOrgRequesterId = otherOrgRequester.Id;
        }

        var requesterContext = new TestCurrentUserContext { UserId = requesterId, OrganisationId = orgId, Role = UserRole.Requester };
        var requestService = new MaintenanceRequestService(sqlite.CreateContext(requesterContext), requesterContext);

        var inRange = await requestService.CreateAsync(siteId, "In range, completed", 100m);
        await requestService.CompleteAsync(inRange.Id, actualCost: 150m);

        var stillOpen = await requestService.CreateAsync(siteId, "Never completed", 200m); // not Completed — must be excluded

        // A request in a different organisation with the same site-naming shouldn't leak in.
        var otherOrgContext = new TestCurrentUserContext { UserId = otherOrgRequesterId, OrganisationId = otherOrgId, Role = UserRole.Requester };
        var otherOrgRequestService = new MaintenanceRequestService(sqlite.CreateContext(otherOrgContext), otherOrgContext);
        var otherOrgRequest = await otherOrgRequestService.CreateAsync(otherOrgSiteId, "Other org", 500m);
        await otherOrgRequestService.CompleteAsync(otherOrgRequest.Id, actualCost: 999m);

        var reportService = new SpendReportService(sqlite.CreateContext(requesterContext), requesterContext);
        var results = await reportService.GetSpendBySiteAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var siteResult = Assert.Single(results);
        Assert.Equal(siteId, siteResult.SiteId);
        Assert.Equal(150m, siteResult.TotalActualCost); // only the completed one counts
        Assert.Equal(1, siteResult.CompletedRequestCount);
        Assert.NotNull(stillOpen);
    }
}

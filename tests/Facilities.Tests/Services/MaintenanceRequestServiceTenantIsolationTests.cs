using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Facilities.Core.Exceptions;
using Facilities.Core.Services;
using Facilities.Tests.TestSupport;

namespace Facilities.Tests.Services;

/// <summary>
/// The test that proves the tenant-isolation claim in DECISIONS.md/README.md: a user
/// from Organisation A cannot read or act on Organisation B's data, even when they
/// have a valid request ID (simulating an ID guessed or copied from another tab).
/// Exercises the real EF global query filter, not a mock of it.
/// </summary>
public class MaintenanceRequestServiceTenantIsolationTests
{
    [Fact]
    public async Task GetByIdAsync_ForRequestBelongingToAnotherOrganisation_ThrowsNotFound()
    {
        using var sqlite = new SqliteTestDatabase();
        var (orgA, orgB, siteA, _, requesterA, _, requesterB, _) = await SeedTwoOrganisationsAsync(sqlite);

        // Requester A raises a request in their own org.
        var requestId = await CreateRequestAsync(sqlite, orgA, siteA, requesterA, estimatedCost: 50m);

        // Requester B (a different organisation entirely) tries to fetch it by ID.
        var orgBContext = new TestCurrentUserContext { UserId = requesterB, OrganisationId = orgB, Role = UserRole.Requester };
        var serviceAsOrgB = new MaintenanceRequestService(sqlite.CreateContext(orgBContext), orgBContext);

        await Assert.ThrowsAsync<NotFoundException>(() => serviceAsOrgB.GetByIdAsync(requestId));
    }

    [Fact]
    public async Task ApproveAsync_ForRequestBelongingToAnotherOrganisation_ThrowsNotFound()
    {
        using var sqlite = new SqliteTestDatabase();
        var (orgA, orgB, siteA, _, requesterA, _, _, approverB) = await SeedTwoOrganisationsAsync(sqlite);

        var requestId = await CreateRequestAsync(sqlite, orgA, siteA, requesterA, estimatedCost: 1000m); // above threshold, pending approval

        // Org B's approver must not even be able to find, let alone approve, Org A's request.
        var orgBContext = new TestCurrentUserContext { UserId = approverB, OrganisationId = orgB, Role = UserRole.Approver };
        var serviceAsOrgB = new MaintenanceRequestService(sqlite.CreateContext(orgBContext), orgBContext);

        await Assert.ThrowsAsync<NotFoundException>(() => serviceAsOrgB.ApproveAsync(requestId));
    }

    [Fact]
    public async Task ListAsync_OnlyReturnsRequestsFromCallersOwnOrganisation()
    {
        using var sqlite = new SqliteTestDatabase();
        var (orgA, orgB, siteA, siteB, requesterA, _, requesterB, _) = await SeedTwoOrganisationsAsync(sqlite);

        await CreateRequestAsync(sqlite, orgA, siteA, requesterA, estimatedCost: 50m);
        await CreateRequestAsync(sqlite, orgB, siteB, requesterB, estimatedCost: 60m);

        var approverAContext = new TestCurrentUserContext { UserId = Guid.NewGuid(), OrganisationId = orgA, Role = UserRole.Approver };
        var serviceAsOrgA = new MaintenanceRequestService(sqlite.CreateContext(approverAContext), approverAContext);

        var visible = await serviceAsOrgA.ListAsync();

        Assert.Single(visible);
        Assert.Equal(orgA, visible[0].OrganisationId);
    }

    [Fact]
    public async Task ApproveAsync_ByRequesterRole_ThrowsForbidden()
    {
        using var sqlite = new SqliteTestDatabase();
        var (orgA, _, siteA, _, requesterA, _, _, _) = await SeedTwoOrganisationsAsync(sqlite);

        var requestId = await CreateRequestAsync(sqlite, orgA, siteA, requesterA, estimatedCost: 1000m);

        var otherRequesterContext = new TestCurrentUserContext { UserId = Guid.NewGuid(), OrganisationId = orgA, Role = UserRole.Requester };
        var service = new MaintenanceRequestService(sqlite.CreateContext(otherRequesterContext), otherRequesterContext);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ApproveAsync(requestId));
    }

    private static async Task<Guid> CreateRequestAsync(SqliteTestDatabase sqlite, Guid orgId, Guid siteId, Guid requesterId, decimal estimatedCost)
    {
        var requesterContext = new TestCurrentUserContext { UserId = requesterId, OrganisationId = orgId, Role = UserRole.Requester };
        var service = new MaintenanceRequestService(sqlite.CreateContext(requesterContext), requesterContext);
        var request = await service.CreateAsync(siteId, "Test request", estimatedCost);
        return request.Id;
    }

    private static async Task<(Guid orgA, Guid orgB, Guid siteA, Guid siteB, Guid requesterA, Guid approverA, Guid requesterB, Guid approverB)>
        SeedTwoOrganisationsAsync(SqliteTestDatabase sqlite)
    {
        using var seedDb = sqlite.CreateContext(new TestCurrentUserContext());

        var orgA = Organisation.Create("Org A", costThresholdAmount: 500m);
        var orgB = Organisation.Create("Org B", costThresholdAmount: 500m);
        seedDb.Organisations.AddRange(orgA, orgB);

        var siteA = Site.Create(orgA.Id, "Site A1");
        var siteB = Site.Create(orgB.Id, "Site B1");
        seedDb.Sites.AddRange(siteA, siteB);

        var requesterA = User.Create(orgA.Id, "requesterA@a.test", "hash", UserRole.Requester);
        var approverA = User.Create(orgA.Id, "approverA@a.test", "hash", UserRole.Approver);
        var requesterB = User.Create(orgB.Id, "requesterB@b.test", "hash", UserRole.Requester);
        var approverB = User.Create(orgB.Id, "approverB@b.test", "hash", UserRole.Approver);
        seedDb.Users.AddRange(requesterA, approverA, requesterB, approverB);

        await seedDb.SaveChangesAsync();

        return (orgA.Id, orgB.Id, siteA.Id, siteB.Id, requesterA.Id, approverA.Id, requesterB.Id, approverB.Id);
    }
}

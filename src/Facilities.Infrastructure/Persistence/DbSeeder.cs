using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Infrastructure.Persistence;

/// <summary>
/// Demo/dev seed data. There's no user-provisioning API (out of scope — see
/// DECISIONS.md), so accounts for logging in during review come from here. Credentials
/// are documented in README.md, not secret, and only ever apply to a local/dev database.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasherService hasher, CancellationToken ct = default)
    {
        if (await db.Organisations.AnyAsync(ct))
            return;

        var acme = Organisation.Create("Acme Facilities Ltd", costThresholdAmount: 500m);
        var northwind = Organisation.Create("Northwind Property Group", costThresholdAmount: 1000m);
        db.Organisations.AddRange(acme, northwind);

        var acmeSite12 = Site.Create(acme.Id, "Site 12 - Riverside Depot");
        var acmeSite7 = Site.Create(acme.Id, "Site 7 - Harbor Warehouse");
        var northwindSite3 = Site.Create(northwind.Id, "Site 3 - Midtown Office");
        var northwindSite9 = Site.Create(northwind.Id, "Site 9 - North Distribution Center");
        db.Sites.AddRange(acmeSite12, acmeSite7, northwindSite3, northwindSite9);

        var hash = hasher.Hash(DemoPassword);
        db.Users.AddRange(
            User.Create(acme.Id, "requester@acme.test", hash, UserRole.Requester),
            User.Create(acme.Id, "approver@acme.test", hash, UserRole.Approver),
            User.Create(northwind.Id, "requester@northwind.test", hash, UserRole.Requester),
            User.Create(northwind.Id, "approver@northwind.test", hash, UserRole.Approver));

        await db.SaveChangesAsync(ct);
    }
}

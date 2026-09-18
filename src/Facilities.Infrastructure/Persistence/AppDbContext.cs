using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Facilities.Infrastructure.Persistence;

/// <summary>
/// Layer 1 of tenant isolation: every tenant-scoped entity gets a global query filter
/// keyed off <see cref="_currentUser"/>. Because the filter expression references an
/// instance field (this._currentUser.OrganisationId) rather than a value captured at
/// model-build time, EF Core re-evaluates it per query against the live value for
/// whichever request this context instance belongs to — the documented EF Core
/// multi-tenancy pattern. Layer 2 (explicit checks in Core services) is the backstop
/// in case a query ever legitimately needs .IgnoreQueryFilters().
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserContext _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Organisation itself is the tenant root — it has no OrganisationId to filter on.
        modelBuilder.Entity<Site>().HasQueryFilter(s => s.OrganisationId == _currentUser.OrganisationId);
        modelBuilder.Entity<User>().HasQueryFilter(u => u.OrganisationId == _currentUser.OrganisationId);
        modelBuilder.Entity<MaintenanceRequest>().HasQueryFilter(r => r.OrganisationId == _currentUser.OrganisationId);
        modelBuilder.Entity<AuditLogEntry>().HasQueryFilter(a => a.OrganisationId == _currentUser.OrganisationId);

        // String check (rather than the IsSqlite() extension) so Infrastructure doesn't
        // need a package reference to the SQLite provider just for this test-only branch.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite has no native DateTimeOffset comparison/ordering support; Postgres
            // (via Npgsql) does, so this conversion is applied only for the SQLite
            // provider used by the test suite. See SqliteTestDatabase in Facilities.Tests.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var dateTimeOffsetProperties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?));

                foreach (var property in dateTimeOffsetProperties)
                {
                    modelBuilder.Entity(entityType.Name).Property(property.Name)
                        .HasConversion(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}

using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Core.Abstractions;

/// <summary>
/// The seam between Core services and EF Core. One interface instead of a
/// repository-per-entity: EF's DbContext already is a Unit of Work + Repository, so
/// wrapping each DbSet again would just be ceremony. This interface exists only so
/// Core doesn't take a package dependency on the Npgsql provider, and so tests can
/// swap in a SQLite-backed implementation.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organisation> Organisations { get; }
    DbSet<Site> Sites { get; }
    DbSet<User> Users { get; }
    DbSet<MaintenanceRequest> MaintenanceRequests { get; }
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

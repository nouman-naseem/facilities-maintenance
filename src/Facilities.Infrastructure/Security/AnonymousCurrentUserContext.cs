using Facilities.Core.Abstractions;
using Facilities.Core.Enums;

namespace Facilities.Infrastructure.Security;

/// <summary>
/// Used only outside an HTTP request — EF design-time tooling (migrations) and
/// startup seeding. OrganisationId is Guid.Empty, which matches no real tenant, so
/// this can never accidentally leak data across tenants; code that needs to read
/// across tenants while using this context (e.g. the seeder) must do so explicitly
/// via .IgnoreQueryFilters() or by querying non-tenant-scoped tables (Organisations).
/// </summary>
public sealed class AnonymousCurrentUserContext : ICurrentUserContext
{
    public Guid UserId => Guid.Empty;
    public Guid OrganisationId => Guid.Empty;
    public UserRole Role => UserRole.Approver;
}

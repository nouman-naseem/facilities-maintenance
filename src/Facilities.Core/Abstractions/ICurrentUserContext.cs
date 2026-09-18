using Facilities.Core.Enums;

namespace Facilities.Core.Abstractions;

/// <summary>
/// The authenticated caller's identity, sourced from the auth ticket (JWT claim or
/// cookie claim — both carry the same claim set) and never from client-supplied
/// request bodies or route values. This is what both the EF global query filter and
/// the explicit tenant checks in services key off.
/// </summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid OrganisationId { get; }
    UserRole Role { get; }
}

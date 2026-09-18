using System.Security.Claims;
using Facilities.Core.Abstractions;
using Facilities.Core.Enums;

namespace Facilities.Api.Security;

/// <summary>
/// Reads the authenticated caller's identity from claims. Works identically whether
/// the request was authenticated via the JWT bearer scheme (JSON API) or the cookie
/// scheme (Razor Pages UI) — both issue the same claim set at login, so this is the
/// one place either front door's identity is translated into the app-level
/// ICurrentUserContext that Core services and the EF query filters rely on.
/// </summary>
public class HttpContextCurrentUserContext : ICurrentUserContext
{
    public const string OrganisationClaimType = "org";

    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal Principal =>
        _accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context is available.");

    public Guid UserId => Guid.Parse(RequireClaim(ClaimTypes.NameIdentifier));
    public Guid OrganisationId => Guid.Parse(RequireClaim(OrganisationClaimType));
    public UserRole Role => Enum.Parse<UserRole>(RequireClaim(ClaimTypes.Role));

    private string RequireClaim(string type) =>
        Principal.FindFirstValue(type) ?? throw new InvalidOperationException($"Missing '{type}' claim on the authenticated principal.");
}

using System.Security.Claims;
using Facilities.Core.Entities;

namespace Facilities.Api.Security;

/// <summary>Builds the one claim set both the JWT scheme and the cookie scheme issue.</summary>
public static class ClaimsFactory
{
    public static Claim[] FromUser(User user) =>
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(HttpContextCurrentUserContext.OrganisationClaimType, user.OrganisationId.ToString()),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(ClaimTypes.Email, user.Email)
    ];
}

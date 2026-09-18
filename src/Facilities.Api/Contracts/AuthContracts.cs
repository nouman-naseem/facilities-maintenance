using System.ComponentModel.DataAnnotations;

namespace Facilities.Api.Contracts;

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record LoginResponse(
    string Token,
    string Email,
    string Role,
    Guid OrganisationId,
    DateTimeOffset ExpiresAt);

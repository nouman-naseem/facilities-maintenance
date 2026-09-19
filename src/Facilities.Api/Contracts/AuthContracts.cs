using System.ComponentModel.DataAnnotations;

namespace Facilities.Api.Contracts;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginResponse(
    string Token,
    string Email,
    string Role,
    Guid OrganisationId,
    DateTimeOffset ExpiresAt);

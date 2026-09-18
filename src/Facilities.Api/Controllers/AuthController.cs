using Facilities.Api.Contracts;
using Facilities.Api.Security;
using Facilities.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facilities.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AuthenticationService _authenticationService;
    private readonly TokenService _tokenService;

    public AuthController(AuthenticationService authenticationService, TokenService tokenService)
    {
        _authenticationService = authenticationService;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _authenticationService.ValidateCredentialsAsync(request.Email, request.Password, ct);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var (token, expiresAt) = _tokenService.CreateToken(user);
        return Ok(new LoginResponse(token, user.Email, user.Role.ToString(), user.OrganisationId, expiresAt));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Facilities.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Facilities.Api.Security;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var signingKey = _config["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var expiryMinutes = _config.GetValue("Jwt:ExpiryMinutes", 480);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: ClaimsFactory.FromUser(user),
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

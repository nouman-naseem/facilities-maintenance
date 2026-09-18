using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Facilities.Infrastructure.Security;

/// <summary>
/// Thin wrapper around ASP.NET Core's PasswordHasher (PBKDF2, per-user random salt,
/// tuned iteration count) — deliberately not the full ASP.NET Core Identity system,
/// which this app doesn't need (no self-registration, no password reset, two roles).
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(default!, password);

    public bool Verify(string hash, string providedPassword) =>
        _hasher.VerifyHashedPassword(default!, hash, providedPassword) != PasswordVerificationResult.Failed;
}

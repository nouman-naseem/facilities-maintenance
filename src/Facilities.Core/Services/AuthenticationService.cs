using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Core.Services;

/// <summary>
/// Shared by both front doors (JWT API login and cookie-based Razor Pages login) so
/// credential-checking logic exists exactly once. Looks a user up by email alone —
/// deliberately bypassing the tenant query filter, since at login time we don't yet
/// know which organisation the caller belongs to (email is globally unique — see
/// UserConfiguration).
/// </summary>
public class AuthenticationService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;

    public AuthenticationService(IAppDbContext db, IPasswordHasherService passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !_passwordHasher.Verify(user.PasswordHash, password))
            return null;

        return user;
    }
}

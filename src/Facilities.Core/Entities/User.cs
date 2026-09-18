using Facilities.Core.Enums;

namespace Facilities.Core.Entities;

public class User
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF Core

    public static User Create(Guid organisationId, string email, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

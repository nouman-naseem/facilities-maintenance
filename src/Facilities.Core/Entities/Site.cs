namespace Facilities.Core.Entities;

public class Site
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string Name { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Site() { } // EF Core

    public static Site Create(Guid organisationId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Site name is required.", nameof(name));

        return new Site
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

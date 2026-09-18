namespace Facilities.Core.Entities;

public class Organisation
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>Requests with EstimatedCost at or above this amount require Approver sign-off.</summary>
    public decimal CostThresholdAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Organisation() { } // EF Core

    public static Organisation Create(string name, decimal costThresholdAmount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organisation name is required.", nameof(name));
        if (costThresholdAmount < 0)
            throw new ArgumentException("Cost threshold cannot be negative.", nameof(costThresholdAmount));

        return new Organisation
        {
            Id = Guid.NewGuid(),
            Name = name,
            CostThresholdAmount = costThresholdAmount,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

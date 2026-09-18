using Facilities.Core.Enums;
using Facilities.Core.Exceptions;

namespace Facilities.Core.Entities;

/// <summary>
/// Owns the request lifecycle state machine. Every transition is a method here so an
/// illegal transition is a compile-time-discoverable, single-place concern rather than
/// something scattered across services or controllers.
/// </summary>
public class MaintenanceRequest
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal EstimatedCost { get; private set; }
    public decimal? ActualCost { get; private set; }
    public RequestStatus Status { get; private set; }

    /// <summary>Null when auto-approved below threshold (no human approver involved).</summary>
    public Guid? ApprovedByUserId { get; private set; }

    /// <summary>Snapshot of the org's threshold at the moment this request was approved/auto-approved.</summary>
    public decimal? ThresholdAtApproval { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private MaintenanceRequest() { } // EF Core

    public static MaintenanceRequest Raise(
        Guid organisationId,
        Guid siteId,
        Guid requestedByUserId,
        string description,
        decimal estimatedCost,
        decimal orgCostThreshold)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (estimatedCost <= 0)
            throw new ArgumentException("Estimated cost must be positive.", nameof(estimatedCost));

        var now = DateTimeOffset.UtcNow;
        var request = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            SiteId = siteId,
            RequestedByUserId = requestedByUserId,
            Description = description.Trim(),
            EstimatedCost = estimatedCost,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (estimatedCost < orgCostThreshold)
        {
            // Below threshold: auto-approved, no human approver.
            request.Status = RequestStatus.Approved;
            request.ThresholdAtApproval = orgCostThreshold;
        }
        else
        {
            request.Status = RequestStatus.PendingApproval;
        }

        return request;
    }

    public bool WasAutoApproved => Status != RequestStatus.PendingApproval && ApprovedByUserId is null && ThresholdAtApproval is not null;

    public void Approve(Guid approverUserId, decimal orgCostThreshold)
    {
        if (Status != RequestStatus.PendingApproval)
            throw new InvalidStateTransitionException($"Cannot approve a request in status '{Status}'.");
        if (approverUserId == RequestedByUserId)
            throw new DomainException("An approver cannot approve their own request.");

        Status = RequestStatus.Approved;
        ApprovedByUserId = approverUserId;
        ThresholdAtApproval = orgCostThreshold;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(Guid approverUserId)
    {
        if (Status != RequestStatus.PendingApproval)
            throw new InvalidStateTransitionException($"Cannot reject a request in status '{Status}'.");
        if (approverUserId == RequestedByUserId)
            throw new DomainException("An approver cannot reject their own request.");

        Status = RequestStatus.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(decimal actualCost, Guid completedByUserId)
    {
        if (Status != RequestStatus.Approved)
            throw new InvalidStateTransitionException($"Cannot complete a request in status '{Status}'.");
        if (actualCost <= 0)
            throw new ArgumentException("Actual cost must be positive.", nameof(actualCost));

        ActualCost = actualCost;
        Status = RequestStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// True when the actual cost, once recorded, exceeds the threshold the request was
    /// approved under. Does not block completion — see DECISIONS.md — but is surfaced
    /// so the audit trail and spend report can flag the variance.
    /// </summary>
    public bool ExceedsApprovedThreshold =>
        ActualCost is not null && ThresholdAtApproval is not null && ActualCost > ThresholdAtApproval;
}

using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Facilities.Core.Exceptions;

namespace Facilities.Tests.Domain;

public class MaintenanceRequestTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();

    [Fact]
    public void Raise_BelowThreshold_AutoApprovesWithoutAnApprover()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Leaky tap", estimatedCost: 100m, orgCostThreshold: 500m);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Null(request.ApprovedByUserId);
        Assert.Equal(500m, request.ThresholdAtApproval);
    }

    [Fact]
    public void Raise_AtOrAboveThreshold_RequiresApproval()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", estimatedCost: 500m, orgCostThreshold: 500m);

        Assert.Equal(RequestStatus.PendingApproval, request.Status);
        Assert.Null(request.ThresholdAtApproval);
    }

    [Fact]
    public void Approve_FromPendingApproval_SetsApprovedAndSnapshotsThreshold()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", 1000m, orgCostThreshold: 500m);

        request.Approve(ApproverId, orgCostThreshold: 500m);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(ApproverId, request.ApprovedByUserId);
        Assert.Equal(500m, request.ThresholdAtApproval);
    }

    [Fact]
    public void Approve_WhenNotPendingApproval_Throws()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Leaky tap", 100m, orgCostThreshold: 500m); // auto-approved

        Assert.Throws<InvalidStateTransitionException>(() => request.Approve(ApproverId, 500m));
    }

    [Fact]
    public void Approve_BySameUserWhoRequested_Throws()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", 1000m, orgCostThreshold: 500m);

        // The core "an approver cannot approve their own request" rule from the brief —
        // enforced in the domain entity so it can never be bypassed regardless of caller.
        var ex = Assert.Throws<DomainException>(() => request.Approve(RequesterId, 500m));
        Assert.Contains("own request", ex.Message);
    }

    [Fact]
    public void Reject_BySameUserWhoRequested_Throws()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", 1000m, orgCostThreshold: 500m);

        Assert.Throws<DomainException>(() => request.Reject(RequesterId));
    }

    [Fact]
    public void Complete_FromApproved_RecordsActualCostAndCompletedAt()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Leaky tap", 100m, orgCostThreshold: 500m); // auto-approved

        request.Complete(actualCost: 120m, completedByUserId: RequesterId);

        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Equal(120m, request.ActualCost);
        Assert.NotNull(request.CompletedAt);
    }

    [Fact]
    public void Complete_WhenNotApproved_Throws()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", 1000m, orgCostThreshold: 500m); // pending approval

        Assert.Throws<InvalidStateTransitionException>(() => request.Complete(1000m, RequesterId));
    }

    [Fact]
    public void ExceedsApprovedThreshold_WhenActualCostExceedsThresholdAtApproval_IsTrue()
    {
        var request = MaintenanceRequest.Raise(OrgId, SiteId, RequesterId, "Roof repair", 1000m, orgCostThreshold: 500m);
        request.Approve(ApproverId, orgCostThreshold: 500m);

        request.Complete(actualCost: 600m, completedByUserId: RequesterId);

        // Business decision: completion is not blocked by the overrun — it's flagged
        // for the audit trail / spend report instead. See DECISIONS.md.
        Assert.True(request.ExceedsApprovedThreshold);
        Assert.Equal(RequestStatus.Completed, request.Status);
    }
}

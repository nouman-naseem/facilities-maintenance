namespace Facilities.Core.Enums;

/// <summary>
/// "Raised" is the transient creation state: every request is routed to either
/// PendingApproval or Approved (auto-approval) in the same operation that creates it,
/// so it is never persisted as a standalone row. See DECISIONS.md.
/// </summary>
public enum RequestStatus
{
    Raised = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Completed = 4
}

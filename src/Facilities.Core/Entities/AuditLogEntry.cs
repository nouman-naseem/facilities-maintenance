using Facilities.Core.Enums;

namespace Facilities.Core.Entities;

/// <summary>
/// Append-only. Nothing in this codebase updates or deletes a row of this type after
/// insert — see DECISIONS.md for how that's enforced beyond "we just don't call it".
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = default!;

    /// <summary>Null for system actions (e.g. auto-approval on request creation).</summary>
    public Guid? PerformedByUserId { get; private set; }

    public RequestStatus? FromStatus { get; private set; }
    public RequestStatus? ToStatus { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AuditLogEntry() { } // EF Core

    public static AuditLogEntry For(
        Guid organisationId,
        string entityType,
        Guid entityId,
        string action,
        Guid? performedByUserId,
        RequestStatus? fromStatus,
        RequestStatus? toStatus,
        string? comment = null)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedByUserId = performedByUserId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Comment = comment,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
    }
}

using System.ComponentModel.DataAnnotations;
using Facilities.Core.Entities;

namespace Facilities.Api.Contracts;

public record CreateRequestDto(
    [property: Required] Guid SiteId,
    [property: Required, StringLength(2000, MinimumLength = 1)] string Description,
    [property: Range(0.01, double.MaxValue, ErrorMessage = "Estimated cost must be positive.")] decimal EstimatedCost);

public record RejectRequestDto(
    [property: StringLength(2000)] string? Reason);

public record CompleteRequestDto(
    [property: Range(0.01, double.MaxValue, ErrorMessage = "Actual cost must be positive.")] decimal ActualCost);

public record MaintenanceRequestDto(
    Guid Id,
    Guid SiteId,
    Guid RequestedByUserId,
    string Description,
    decimal EstimatedCost,
    decimal? ActualCost,
    string Status,
    Guid? ApprovedByUserId,
    decimal? ThresholdAtApproval,
    bool ExceedsApprovedThreshold,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt)
{
    public static MaintenanceRequestDto FromEntity(MaintenanceRequest r) => new(
        r.Id, r.SiteId, r.RequestedByUserId, r.Description, r.EstimatedCost, r.ActualCost,
        r.Status.ToString(), r.ApprovedByUserId, r.ThresholdAtApproval, r.ExceedsApprovedThreshold,
        r.CreatedAt, r.UpdatedAt, r.CompletedAt);
}

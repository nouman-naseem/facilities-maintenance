using Facilities.Api.Contracts;
using Facilities.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facilities.Api.Controllers;

[Route("api/requests")]
public class MaintenanceRequestsController : ApiControllerBase
{
    private readonly MaintenanceRequestService _requests;

    public MaintenanceRequestsController(MaintenanceRequestService requests)
    {
        _requests = requests;
    }

    [HttpGet]
    public async Task<ActionResult<List<MaintenanceRequestDto>>> List(CancellationToken ct)
    {
        var requests = await _requests.ListAsync(ct);
        return Ok(requests.Select(MaintenanceRequestDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceRequestDto>> GetById(Guid id, CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(id, ct);
        return Ok(MaintenanceRequestDto.FromEntity(request));
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceRequestDto>> Create(CreateRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.CreateAsync(dto.SiteId, dto.Description, dto.EstimatedCost, ct);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, MaintenanceRequestDto.FromEntity(request));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Approver")]
    public async Task<ActionResult<MaintenanceRequestDto>> Approve(Guid id, CancellationToken ct)
    {
        var request = await _requests.ApproveAsync(id, ct);
        return Ok(MaintenanceRequestDto.FromEntity(request));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Approver")]
    public async Task<ActionResult<MaintenanceRequestDto>> Reject(Guid id, RejectRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.RejectAsync(id, dto.Reason, ct);
        return Ok(MaintenanceRequestDto.FromEntity(request));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MaintenanceRequestDto>> Complete(Guid id, CompleteRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.CompleteAsync(id, dto.ActualCost, ct);
        return Ok(MaintenanceRequestDto.FromEntity(request));
    }
}

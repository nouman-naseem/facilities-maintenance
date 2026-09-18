using System.ComponentModel.DataAnnotations;
using Facilities.Core.Abstractions;
using Facilities.Core.Entities;
using Facilities.Core.Enums;
using Facilities.Core.Exceptions;
using Facilities.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Api.Pages.Requests;

public class IndexModel : PageModel
{
    private readonly MaintenanceRequestService _requests;
    private readonly IAppDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public IndexModel(MaintenanceRequestService requests, IAppDbContext db, ICurrentUserContext currentUser)
    {
        _requests = requests;
        _db = db;
        _currentUser = currentUser;
    }

    public List<MaintenanceRequest> Requests { get; private set; } = new();
    public List<Site> Sites { get; private set; } = new();
    public Dictionary<Guid, string> UserEmailsById { get; private set; } = new();
    public bool IsApprover => _currentUser.Role == UserRole.Approver;
    public Guid CurrentUserId => _currentUser.UserId;

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public string? Error { get; set; }

    [BindProperty]
    public CreateInput NewRequest { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(ct);
            return Page();
        }

        try
        {
            await _requests.CreateAsync(NewRequest.SiteId, NewRequest.Description, NewRequest.EstimatedCost, ct);
            Message = "Request submitted.";
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _requests.ApproveAsync(id, ct);
            Message = "Request approved.";
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? reason, CancellationToken ct)
    {
        try
        {
            await _requests.RejectAsync(id, reason, ct);
            Message = "Request rejected.";
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid id, decimal actualCost, CancellationToken ct)
    {
        try
        {
            await _requests.CompleteAsync(id, actualCost, ct);
            Message = "Request marked complete.";
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Requests = await _requests.ListAsync(ct);
        Sites = await _db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        UserEmailsById = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Email, ct);
    }

    public class CreateInput
    {
        [Required]
        public Guid SiteId { get; set; }

        [Required, StringLength(2000, MinimumLength = 1)]
        public string Description { get; set; } = "";

        [Range(0.01, double.MaxValue, ErrorMessage = "Estimated cost must be positive.")]
        public decimal EstimatedCost { get; set; }
    }
}

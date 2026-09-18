using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Facilities.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CoreAuthenticationService = Facilities.Core.Services.AuthenticationService;

namespace Facilities.Api.Pages;

public class LoginModel : PageModel
{
    private readonly CoreAuthenticationService _authenticationService;

    public LoginModel(CoreAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _authenticationService.ValidateCredentialsAsync(Input.Email, Input.Password, ct);
        if (user is null)
        {
            Error = "Invalid email or password.";
            return Page();
        }

        var identity = new ClaimsIdentity(ClaimsFactory.FromUser(user), CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Requests/Index");
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }
}

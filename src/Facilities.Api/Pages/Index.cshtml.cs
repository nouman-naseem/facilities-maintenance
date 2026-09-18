using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Facilities.Api.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() =>
        RedirectToPage(User.Identity?.IsAuthenticated == true ? "/Requests/Index" : "/Login");
}

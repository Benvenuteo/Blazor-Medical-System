using MedicalBookingSystem.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MedicalBookingSystem.BlazorServer.Pages.Account;

public sealed class LogoutAllModel(SessionManagementService sessions) : PageModel
{
    public string? Error { get; private set; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        var result = await sessions.RevokeAllAsync(User);
        if (!result.IsSuccess) { Error = result.Error; return Page(); }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/Account/Login");
    }
}

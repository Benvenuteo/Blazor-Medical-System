using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MedicalBookingSystem.BlazorServer.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("authentication")]
public sealed class LoginModel(StaffAuthenticationService authentication) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(255)] public string Email { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(128)] public string Password { get; set; } = string.Empty;
    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await authentication.AuthenticateAsync(new LoginDto { Email = Email, Password = Password });
        if (user is null)
        {
            Error = "Nieprawidłowy email lub hasło.";
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
        return LocalRedirect(user.HasClaim(MedicalBookingSystem.SharedKernel.Security.AccountClaims.PasswordChangeRequired, "true")
            ? "/Account/ChangePassword" : "/");
    }
}

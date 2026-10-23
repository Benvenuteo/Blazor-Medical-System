using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MedicalBookingSystem.BlazorServer.Pages.Account;

[Authorize(Roles = Roles.Staff)]
public sealed class ChangePasswordModel(StaffPasswordService passwords) : PageModel
{
    [BindProperty] public ChangeStaffPasswordDto Input { get; set; } = new();
    public string? Error { get; private set; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        var result = await passwords.ChangeAsync(User, Input);
        if (!result.IsSuccess) { Error = result.Error; return Page(); }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/Account/Login?passwordChanged=true");
    }
}

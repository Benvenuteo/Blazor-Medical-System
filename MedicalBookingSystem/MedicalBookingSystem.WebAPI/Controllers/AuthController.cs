using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;
using FluentValidation;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.WebAPI.JWT;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedicalBookingSystem.WebAPI.Controllers;

[Route("api/auth")]
[ApiController]
[EnableRateLimiting("authentication")]
public sealed class AuthController(PatientAuthenticationService patients, IValidator<LoginDto> loginValidator,
    JwtService jwt, StaffAuthenticationService staff, StaffPasswordService passwords, SessionManagementService sessions,
    AccountRecoveryService recovery) : ControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterPatientDto dto)
    {
        var result = await patients.RegisterAsync(dto);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value.Principal), result.Value.Patient)) : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await patients.LoginAsync(dto);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value.Principal), result.Value.Patient)) : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("staff/login")]
    public async Task<IActionResult> StaffLogin(LoginDto dto)
    {
        var validation = await loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Problem(statusCode: 400, detail: string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        var principal = await staff.AuthenticateAsync(dto);
        return principal is null ? Problem(statusCode: 401, detail: LoginThrottle.FailureMessage) : Ok(new TokenResponseDto(jwt.GenerateToken(principal)));
    }

    [Authorize(Roles = Roles.Staff), HttpPost("staff/change-password")]
    public async Task<IActionResult> ChangeStaffPassword(ChangeStaffPasswordDto dto)
    {
        var result = await passwords.ChangeAsync(User, dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    /// <summary>Always 202 for a valid address, whether or not an account exists, so the endpoint cannot reveal accounts.</summary>
    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var result = await recovery.RequestPasswordResetAsync(dto);
        return result.IsSuccess ? Accepted() : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var result = await recovery.ResetPasswordAsync(dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto dto)
    {
        var result = await recovery.ConfirmEmailAsync(dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    [Authorize(Roles = Roles.Patient), HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromServices] UserAccess access)
    {
        var result = await recovery.ResendConfirmationAsync(await access.PatientIdAsync());
        return result.IsSuccess ? Accepted() : this.FailureResponse(result);
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var result = await sessions.RevokeAllAsync(User);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}

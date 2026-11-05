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
    AccountRecoveryService recovery, ExternalAuthenticationService external) : ControllerBase
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

    [AllowAnonymous, HttpPost("google")]
    public async Task<IActionResult> GoogleSignIn(GoogleSignInDto dto)
    {
        var result = await external.SignInWithGoogleAsync(dto);
        if (result.IsSuccess) return Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value.Principal), result.Value.Patient));
        if (result.Error == ExternalLoginErrors.AccountExists)
            return Problem(statusCode: 409, title: ExternalLoginErrors.AccountExists,
                detail: "Konto z tym adresem email już istnieje. Zaloguj się hasłem i połącz konto Google w ustawieniach bezpieczeństwa.");
        return this.FailureResponse(result);
    }

    [Authorize(Roles = Roles.Patient), HttpGet("google/status")]
    public async Task<IActionResult> GoogleStatus()
    {
        var result = await external.StatusAsync();
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [Authorize(Roles = Roles.Patient), HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle(LinkGoogleDto dto)
    {
        var result = await external.LinkGoogleAsync(dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    [Authorize(Roles = Roles.Patient), HttpDelete("google/link")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var result = await external.UnlinkGoogleAsync();
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    /// <summary>Completes a Google sign-up and returns a new token without the profile flag.</summary>
    [Authorize(Roles = Roles.Patient), HttpPut("profile/complete")]
    public async Task<IActionResult> CompleteProfile(CompleteProfileDto dto)
    {
        var result = await external.CompleteProfileAsync(dto);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value.Principal), result.Value.Patient)) : this.FailureResponse(result);
    }

    /// <summary>Renews the patient token while the session is valid (max 12 hours from sign-in).</summary>
    [Authorize(Roles = Roles.Patient), HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var result = await patients.RefreshAsync(User);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value.Principal), result.Value.Patient)) : this.FailureResponse(result);
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var result = await sessions.RevokeAllAsync(User);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}

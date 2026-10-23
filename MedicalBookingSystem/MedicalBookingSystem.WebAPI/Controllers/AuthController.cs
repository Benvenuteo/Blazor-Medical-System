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
    JwtService jwt, StaffAuthenticationService staff, StaffPasswordService passwords, SessionManagementService sessions) : ControllerBase
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
        return principal is null ? Problem(statusCode: 401, detail: "Nieprawidłowy email lub hasło.") : Ok(new TokenResponseDto(jwt.GenerateToken(principal)));
    }

    [Authorize(Roles = Roles.Staff), HttpPost("staff/change-password")]
    public async Task<IActionResult> ChangeStaffPassword(ChangeStaffPasswordDto dto)
    {
        var result = await passwords.ChangeAsync(User, dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var result = await sessions.RevokeAllAsync(User);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}

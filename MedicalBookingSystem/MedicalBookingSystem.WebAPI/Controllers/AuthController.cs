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
public sealed class AuthController(IPatientService patients, IValidator<LoginDto> loginValidator,
    JwtService jwt, StaffAuthenticationService staff) : ControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterPatientDto dto)
    {
        var result = await patients.RegisterAsync(dto);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value), result.Value)) : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await patients.LoginAsync(dto);
        return result.IsSuccess ? Ok(new AuthenticationResponseDto(jwt.GenerateToken(result.Value), result.Value)) : this.FailureResponse(result);
    }

    [AllowAnonymous, HttpPost("staff/login")]
    public async Task<IActionResult> StaffLogin(LoginDto dto)
    {
        var validation = await loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Problem(statusCode: 400, detail: string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        var principal = await staff.AuthenticateAsync(dto);
        return principal is null ? Problem(statusCode: 401, detail: "Nieprawidłowy email lub hasło.") : Ok(new TokenResponseDto(jwt.GenerateToken(principal)));
    }
}

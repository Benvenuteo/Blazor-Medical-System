using Microsoft.AspNetCore.RateLimiting;
using MedicalBookingSystem.Application.Security;
using Microsoft.AspNetCore.Authorization;
using MedicalBookingSystem.Application.Mappings;
using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.WebAPI.JWT;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Route("api/auth")]
[ApiController]
[EnableRateLimiting("authentication")]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Patient> _passwordHasher;
    private readonly IValidator<RegisterPatientDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;

    private readonly JwtService _jwtService;
    private readonly StaffAuthenticationService _staffAuthentication;

    public AuthController(
        IUnitOfWork uow,
        IPasswordHasher<Patient> passwordHasher,
        IValidator<RegisterPatientDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        JwtService jwtService, StaffAuthenticationService staffAuthentication)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;

        _jwtService = jwtService;
        _staffAuthentication = staffAuthentication;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPatientDto dto)
    {
        var validationResult = await _registerValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

        var existing = await _uow.Patients.GetByEmailAsync(dto.Email);
        if (existing != null)
            return BadRequest("Użytkownik z podanym adresem e-mail już istnieje.");

        var patient = new Patient { FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim(), Email = dto.Email.Trim().ToLowerInvariant(), PhoneNumber = dto.PhoneNumber, DateOfBirth = dto.DateOfBirth };
        patient.PasswordHash = _passwordHasher.HashPassword(patient, dto.Password);

        await _uow.Patients.AddAsync(patient);
        await _uow.SaveChangesAsync();

        var patientDto = patient.ToDto();
        var token = _jwtService.GenerateToken(patient);

        return Ok(new { token, patient = patientDto });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var validationResult = await _loginValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

        var patient = await _uow.Patients.GetByEmailAsync(dto.Email);
        if (patient == null)
            return Unauthorized("Nieprawidłowy email lub hasło");

        var result = _passwordHasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Nieprawidłowy email lub hasło");

        var patientDto = patient.ToDto();
        var token = _jwtService.GenerateToken(patient);

        return Ok(new { token, patient = patientDto });
    }
    [AllowAnonymous]
    [HttpPost("staff/login")]
    public async Task<IActionResult> StaffLogin([FromBody] LoginDto dto)
    {
        var validation = await _loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        var principal = await _staffAuthentication.AuthenticateAsync(dto);
        return principal is null ? Unauthorized("Nieprawidłowy email lub hasło") : Ok(new { token = _jwtService.GenerateToken(principal) });
    }
}

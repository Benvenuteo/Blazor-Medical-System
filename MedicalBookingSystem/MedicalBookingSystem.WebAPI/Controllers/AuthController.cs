using AutoMapper;
using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.WebAPI.JWT;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Mvc;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<RegisterPatientDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IMapper _mapper;
    private readonly JwtService _jwtService;

    public AuthController(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IValidator<RegisterPatientDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IMapper mapper,
        JwtService jwtService)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _mapper = mapper;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPatientDto dto)
    {
        var validationResult = await _registerValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

        var existing = await _uow.Patients.GetByEmailAsync(dto.Email);
        if (existing != null)
            return BadRequest("Użytkownik z podanym adresem e-mail już istnieje.");

        var patient = _mapper.Map<Patient>(dto);
        patient.PasswordHash = _passwordHasher.HashPassword(dto.Password);

        await _uow.Patients.AddAsync(patient);
        await _uow.SaveChangesAsync();

        var patientDto = _mapper.Map<PatientDto>(patient);
        var token = _jwtService.GenerateToken(patient);

        return Ok(new { token, patient = patientDto });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var validationResult = await _loginValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

        var patient = await _uow.Patients.GetByEmailAsync(dto.Email);
        if (patient == null)
            return Unauthorized("Nieprawidłowy email lub hasło");

        var result = _passwordHasher.VerifyHashedPassword(patient.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Nieprawidłowy email lub hasło");

        var patientDto = _mapper.Map<PatientDto>(patient);
        var token = _jwtService.GenerateToken(patient);

        return Ok(new { token, patient = patientDto });
    }
}

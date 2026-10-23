using System.Security.Claims;
using FluentValidation;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Security;

public sealed record AuthenticatedPatient(PatientDto Patient, ClaimsPrincipal Principal);

public sealed class PatientAuthenticationService(IUnitOfWorkFactory units, IPasswordHasher<Patient> hasher, ClinicClock clock,
    IValidator<RegisterPatientDto> registerValidator, IValidator<LoginDto> loginValidator)
{
    public async Task<Result<AuthenticatedPatient>> LoginAsync(LoginDto dto)
    {
        var validation = await loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<AuthenticatedPatient>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var patient = await unit.Patients.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        if (patient is null || hasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Result.Failure<AuthenticatedPatient>("Nieprawidłowy email lub hasło.", ErrorType.Unauthorized);
        return Result.Success(CreateSession(patient));
    }

    public async Task<Result<AuthenticatedPatient>> RegisterAsync(RegisterPatientDto dto)
    {
        var validation = await registerValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<AuthenticatedPatient>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await unit.Patients.GetByEmailAsync(email) is not null)
            return Result.Failure<AuthenticatedPatient>("Email jest już zajęty.", ErrorType.Conflict);
        var patient = new Patient { FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim(), Email = email,
            PhoneNumber = dto.PhoneNumber, DateOfBirth = dto.DateOfBirth };
        patient.PasswordHash = hasher.HashPassword(patient, dto.Password);
        await unit.Patients.AddAsync(patient);
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException ex) when (ex.Kind == PersistenceConflictKind.DuplicateAccount)
        { return Result.Failure<AuthenticatedPatient>("Email jest już zajęty.", ErrorType.Conflict); }
        return Result.Success(CreateSession(patient));
    }


    private AuthenticatedPatient CreateSession(Patient patient) => new(patient.ToDto(), AccountClaims.Create(patient.Id,
        $"{patient.FirstName} {patient.LastName}", patient.Email, Roles.Patient, new DateTimeOffset(clock.UtcNow).AddHours(1), securityStamp: patient.SecurityStamp));
}

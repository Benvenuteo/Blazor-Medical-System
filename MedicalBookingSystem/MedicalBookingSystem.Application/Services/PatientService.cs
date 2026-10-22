using FluentValidation;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Services;

public sealed class PatientService(IUnitOfWorkFactory units, IPasswordHasher<Patient> hasher, UserAccess access,
    IValidator<RegisterPatientDto> registerValidator, IValidator<LoginDto> loginValidator,
    IValidator<UpdatePatientDto> updateValidator) : IPatientService
{
    public async Task<Result<PatientDto>> GetPatientProfileAsync(int id)
    {
        await access.RequirePatientAsync(id);
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(id);
        return patient is null ? Result.Failure<PatientDto>("Pacjent nie został znaleziony.", ErrorType.NotFound) : Result.Success(patient.ToDto());
    }

    public async Task<Result<PatientDto>> LoginAsync(LoginDto dto)
    {
        var validation = await loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<PatientDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var patient = await unit.Patients.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        if (patient is null || hasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Result.Failure<PatientDto>("Nieprawidłowy email lub hasło.", ErrorType.Unauthorized);
        return Result.Success(patient.ToDto());
    }

    public async Task<Result<PatientDto>> RegisterAsync(RegisterPatientDto dto)
    {
        var validation = await registerValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<PatientDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await unit.Patients.GetByEmailAsync(email) is not null)
            return Result.Failure<PatientDto>("Email jest już zajęty.", ErrorType.Conflict);
        var patient = new Patient { FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim(), Email = email,
            PhoneNumber = dto.PhoneNumber, DateOfBirth = dto.DateOfBirth };
        patient.PasswordHash = hasher.HashPassword(patient, dto.Password);
        await unit.Patients.AddAsync(patient);
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException ex) when (ex.Kind == PersistenceConflictKind.DuplicateAccount)
        { return Result.Failure<PatientDto>("Email jest już zajęty.", ErrorType.Conflict); }
        return Result.Success(patient.ToDto());
    }

    public async Task<Result> UpdatePatientAsync(int id, UpdatePatientDto dto)
    {
        await access.RequirePatientAsync(id);
        var validation = await updateValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(id);
        if (patient is null) return Result.Failure("Pacjent nie został znaleziony.", ErrorType.NotFound);
        // Verify credentials before changing any tracked state.
        if (!string.IsNullOrEmpty(dto.NewPassword))
        {
            if (string.IsNullOrEmpty(dto.CurrentPassword) ||
                hasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
                return Result.Failure("Aktualne hasło jest nieprawidłowe.");
            patient.PasswordHash = hasher.HashPassword(patient, dto.NewPassword);
        }
        patient.FirstName = dto.FirstName?.Trim() ?? patient.FirstName;
        patient.LastName = dto.LastName?.Trim() ?? patient.LastName;
        patient.PhoneNumber = dto.PhoneNumber ?? patient.PhoneNumber;
        await unit.SaveChangesAsync();
        return Result.Success();
    }
}

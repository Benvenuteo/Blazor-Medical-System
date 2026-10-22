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
    IValidator<UpdatePatientDto> updateValidator) : IPatientService
{
    public async Task<Result<PatientDto>> GetPatientProfileAsync(int id)
    {
        await access.RequirePatientAsync(id);
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(id);
        return patient is null ? Result.Failure<PatientDto>("Pacjent nie został znaleziony.", ErrorType.NotFound) : Result.Success(patient.ToDto());
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
            patient.SecurityStamp = Guid.NewGuid();
        }
        patient.FirstName = dto.FirstName?.Trim() ?? patient.FirstName;
        patient.LastName = dto.LastName?.Trim() ?? patient.LastName;
        patient.PhoneNumber = dto.PhoneNumber ?? patient.PhoneNumber;
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException) { return Result.Failure("Konto zmieniło się w innym żądaniu. Zaloguj się ponownie i spróbuj jeszcze raz.", ErrorType.Conflict); }
        return Result.Success();
    }
}

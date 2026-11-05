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
        var now = clock.UtcNow;
        // Every path computes exactly one password hash, so response time does not reveal whether the email exists.
        if (patient is null || LoginThrottle.IsLockedOut(patient.LockoutEndUtc, patient.IsDemo, now))
        {
            hasher.VerifyHashedPassword(TimingDummy, DummyHash(), dto.Password);
            return Result.Failure<AuthenticatedPatient>(LoginThrottle.FailureMessage, ErrorType.Unauthorized);
        }
        var failed = hasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.Password) == PasswordVerificationResult.Failed;
        if (failed || patient.FailedLoginCount != 0 || patient.LockoutEndUtc is not null)
        {
            // The email lookup is read-only; counters are updated on a tracked copy of the account.
            var tracked = await unit.Patients.GetByIdAsync(patient.Id);
            if (tracked is not null)
            {
                (tracked.FailedLoginCount, tracked.LockoutEndUtc) = failed
                    ? LoginThrottle.RegisterFailure(tracked.FailedLoginCount, tracked.IsDemo, now) : (0, null);
                await SaveCountersAsync(unit);
            }
        }
        if (failed) return Result.Failure<AuthenticatedPatient>(LoginThrottle.FailureMessage, ErrorType.Unauthorized);
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


    private static readonly Patient TimingDummy = new();
    private static string? dummyHash;
    private string DummyHash() => dummyHash ??= hasher.HashPassword(TimingDummy, Guid.NewGuid().ToString("N"));

    /// <summary>Counters are best effort: a concurrent update of the same account must not fail the login response.</summary>
    private static async Task SaveCountersAsync(IUnitOfWork unit)
    {
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException) { }
    }

    private AuthenticatedPatient CreateSession(Patient patient) => new(patient.ToDto(), AccountClaims.Create(patient.Id,
        $"{patient.FirstName} {patient.LastName}", patient.Email, Roles.Patient, new DateTimeOffset(clock.UtcNow).AddHours(1), securityStamp: patient.SecurityStamp, demoAccount: patient.IsDemo));
}

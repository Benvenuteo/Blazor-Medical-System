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
    IValidator<RegisterPatientDto> registerValidator, IValidator<LoginDto> loginValidator, AccountRecoveryService recovery,
    SessionGuard guard)
{
    public async Task<Result<AuthenticatedPatient>> LoginAsync(LoginDto dto)
    {
        var validation = await loginValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<AuthenticatedPatient>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var patient = await unit.Patients.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        var now = clock.UtcNow;
        // Every path computes exactly one password hash, so response time does not reveal whether the email exists.
        // Accounts created through Google have no password until the owner sets one via the reset link.
        if (patient?.PasswordHash is null || LoginThrottle.IsLockedOut(patient.LockoutEndUtc, patient.IsDemo, now))
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
        await recovery.QueueConfirmationAsync(unit, patient);
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

    /// <summary>One access token lives an hour; the app renews it while open, up to this limit since sign-in.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(12);

    /// <summary>Builds the signed-in session for any patient sign-in method (password, Google, profile completion, renewal).</summary>
    public AuthenticatedPatient CreateSession(Patient patient, DateTimeOffset? sessionStarted = null)
    {
        var now = new DateTimeOffset(clock.UtcNow);
        var started = sessionStarted ?? now;
        // The token never outlives the session limit.
        var expires = now + TokenLifetime < started + MaxSessionLength ? now + TokenLifetime : started + MaxSessionLength;
        return new(patient.ToDto(), AccountClaims.Create(patient.Id, $"{patient.FirstName} {patient.LastName}", patient.Email,
            Roles.Patient, expires, securityStamp: patient.SecurityStamp, demoAccount: patient.IsDemo,
            profileIncomplete: patient.ProfileIncomplete, sessionStarted: started));
    }

    /// <summary>
    /// Exchanges a still-valid token for a fresh one. The database session check runs first, so a changed password,
    /// "log out everywhere" or a deleted account stop renewal; the original sign-in time caps the session length.
    /// </summary>
    public async Task<Result<AuthenticatedPatient>> RefreshAsync(ClaimsPrincipal user)
    {
        await guard.RequireAsync(user, allowIncompleteProfile: true);
        if (!AccountClaims.TryReadIdentity(user, out var identity) || identity.Role != Roles.Patient) throw new AccessDeniedException(true);
        if (!long.TryParse(user.FindFirst(AccountClaims.SessionStarted)?.Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var startedSeconds))
            return Result.Failure<AuthenticatedPatient>("Sesja wymaga ponownego logowania.", ErrorType.Unauthorized);
        var started = DateTimeOffset.FromUnixTimeSeconds(startedSeconds);
        var now = new DateTimeOffset(clock.UtcNow);
        if (started > now || now - started >= MaxSessionLength - TimeSpan.FromMinutes(1))
            return Result.Failure<AuthenticatedPatient>("Sesja wygasła. Zaloguj się ponownie.", ErrorType.Unauthorized);
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(identity.Id);
        return patient is null
            ? Result.Failure<AuthenticatedPatient>("Sesja wymaga ponownego logowania.", ErrorType.Unauthorized)
            : Result.Success(CreateSession(patient, started));
    }
}

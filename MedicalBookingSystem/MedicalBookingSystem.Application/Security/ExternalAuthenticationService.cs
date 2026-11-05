using FluentValidation;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Security;

/// <summary>Identity confirmed by the provider: signature, issuer, audience, lifetime and nonce are already checked.</summary>
public sealed record ExternalIdentity(string Provider, string Subject, string Email, bool EmailVerified, string? GivenName, string? FamilyName, string? Name);

public interface IExternalTokenValidator
{
    bool GoogleEnabled { get; }
    /// <returns>The identity, or null when the token is invalid, expired, for another client or has a different nonce.</returns>
    Task<ExternalIdentity?> ValidateGoogleAsync(string idToken, string nonce, CancellationToken cancellationToken = default);
}

/// <summary>
/// Google sign-in for patients. The API trusts only a verified Google ID token and then issues its own JWT, so roles,
/// session revocation and the profile gate work exactly like password sign-in.
/// Accounts are matched by the stable Google subject, never by email alone:
/// an existing confirmed account must be linked by its owner (with the current password);
/// an unconfirmed registration of the same address is taken over, because Google has verified that mailbox.
/// </summary>
public sealed class ExternalAuthenticationService(IUnitOfWorkFactory units, IExternalTokenValidator tokens,
    PatientAuthenticationService sessions, IPasswordHasher<Patient> hasher, ClinicClock clock, UserAccess access,
    IValidator<CompleteProfileDto> profileValidator)
{
    public const string Google = "Google";
    private const string InvalidToken = "Nie udało się potwierdzić logowania Google. Spróbuj ponownie.";
    private const string Disabled = "Logowanie przez Google nie jest skonfigurowane.";

    public async Task<Result<AuthenticatedPatient>> SignInWithGoogleAsync(GoogleSignInDto dto)
    {
        if (!tokens.GoogleEnabled) return Result.Failure<AuthenticatedPatient>(Disabled, ErrorType.NotFound);
        var identity = await tokens.ValidateGoogleAsync(dto.IdToken, dto.Nonce);
        if (identity is null || !identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
            return Result.Failure<AuthenticatedPatient>(InvalidToken, ErrorType.Unauthorized);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var unit = units.Create();
            var link = (await unit.ExternalLogins.FindAsync(l => l.Provider == Google && l.Subject == identity.Subject)).SingleOrDefault();
            if (link is not null)
            {
                var linked = await unit.Patients.GetByIdAsync(link.PatientId);
                return linked is null ? Result.Failure<AuthenticatedPatient>(InvalidToken, ErrorType.Unauthorized) : Result.Success(sessions.CreateSession(linked));
            }
            var email = identity.Email.Trim().ToLowerInvariant();
            var byEmail = await unit.Patients.GetByEmailAsync(email);
            Patient patient;
            if (byEmail is not null)
            {
                if (byEmail.IsDemo) return DemoAccounts.Refused<AuthenticatedPatient>();
                if (byEmail.EmailConfirmed)
                    return Result.Failure<AuthenticatedPatient>(ExternalLoginErrors.AccountExists, ErrorType.Conflict);
                // Unconfirmed registration of an address Google has verified: the mailbox owner takes the account over,
                // and the password chosen by whoever registered it stops working.
                patient = (await unit.Patients.GetByIdAsync(byEmail.Id))!;
                patient.PasswordHash = null;
                patient.EmailConfirmed = true;
                patient.SecurityStamp = Guid.NewGuid();
                patient.FailedLoginCount = 0;
                patient.LockoutEndUtc = null;
            }
            else
            {
                var (first, last) = Names(identity);
                patient = new Patient
                {
                    Email = email, FirstName = first, LastName = last, PhoneNumber = string.Empty,
                    EmailConfirmed = true, ProfileIncomplete = true
                };
                await unit.Patients.AddAsync(patient);
            }
            await unit.ExternalLogins.AddAsync(new PatientExternalLogin
                { Patient = patient, Provider = Google, Subject = identity.Subject, Email = email, LinkedAtUtc = clock.UtcNow });
            try
            {
                await unit.SaveChangesAsync();
                return Result.Success(sessions.CreateSession(patient));
            }
            catch (PersistenceConflictException exception) when (exception.Kind is PersistenceConflictKind.DuplicateExternalLogin
                or PersistenceConflictKind.DuplicateAccount or PersistenceConflictKind.ConcurrentChange)
            {
                // A parallel first sign-in won the race; the second pass finds its link.
            }
        }
        return Result.Failure<AuthenticatedPatient>(InvalidToken, ErrorType.Conflict);
    }

    public async Task<Result> LinkGoogleAsync(LinkGoogleDto dto)
    {
        if (!tokens.GoogleEnabled) return Result.Failure(Disabled, ErrorType.NotFound);
        var patientId = await access.PatientIdAsync();
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(patientId);
        if (patient is null) return Result.Failure("Pacjent nie został znaleziony.", ErrorType.NotFound);
        if (patient.IsDemo) return DemoAccounts.Refused();
        // A stolen browser token alone must not be enough to attach an attacker's Google account permanently.
        if (patient.PasswordHash is null || string.IsNullOrEmpty(dto.CurrentPassword) ||
            hasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result.Failure("Aktualne hasło jest nieprawidłowe.");
        var identity = await tokens.ValidateGoogleAsync(dto.IdToken, dto.Nonce);
        if (identity is null || !identity.EmailVerified) return Result.Failure(InvalidToken, ErrorType.Unauthorized);
        if (await unit.ExternalLogins.AnyAsync(l => l.PatientId == patientId && l.Provider == Google))
            return Result.Failure("Konto Google jest już połączone.", ErrorType.Conflict);
        if (await unit.ExternalLogins.AnyAsync(l => l.Provider == Google && l.Subject == identity.Subject))
            return Result.Failure("To konto Google jest połączone z innym pacjentem.", ErrorType.Conflict);
        await unit.ExternalLogins.AddAsync(new PatientExternalLogin
            { PatientId = patientId, Provider = Google, Subject = identity.Subject, Email = identity.Email.Trim().ToLowerInvariant(), LinkedAtUtc = clock.UtcNow });
        return await SaveAsync(unit, "To konto Google jest połączone z innym pacjentem.");
    }

    public async Task<Result> UnlinkGoogleAsync()
    {
        var patientId = await access.PatientIdAsync();
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(patientId);
        if (patient is null) return Result.Failure("Pacjent nie został znaleziony.", ErrorType.NotFound);
        if (patient.IsDemo) return DemoAccounts.Refused();
        if (patient.PasswordHash is null)
            return Result.Failure("Najpierw ustaw hasło (opcja „Nie pamiętasz hasła?”), aby nie stracić dostępu do konta.", ErrorType.Conflict);
        var link = (await unit.ExternalLogins.FindAsync(l => l.PatientId == patientId && l.Provider == Google)).SingleOrDefault();
        if (link is null) return Result.Success();
        unit.ExternalLogins.Remove((await unit.ExternalLogins.GetByIdAsync(link.Id))!);
        return await SaveAsync(unit, "Konto zmieniło się w międzyczasie.");
    }

    public async Task<Result<ExternalLoginStatusDto>> StatusAsync()
    {
        var patientId = await access.PatientIdAsync(allowIncompleteProfile: true);
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(patientId);
        if (patient is null) return Result.Failure<ExternalLoginStatusDto>("Pacjent nie został znaleziony.", ErrorType.NotFound);
        var link = (await unit.ExternalLogins.FindAsync(l => l.PatientId == patientId && l.Provider == Google)).SingleOrDefault();
        return Result.Success(new ExternalLoginStatusDto(tokens.GoogleEnabled, link is not null, link?.Email, patient.PasswordHash is not null));
    }

    /// <summary>Completes an external sign-up; returns a fresh session without the profile flag.</summary>
    public async Task<Result<AuthenticatedPatient>> CompleteProfileAsync(CompleteProfileDto dto)
    {
        var patientId = await access.PatientIdAsync(allowIncompleteProfile: true);
        var validation = await profileValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<AuthenticatedPatient>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(patientId);
        if (patient is null) return Result.Failure<AuthenticatedPatient>("Pacjent nie został znaleziony.", ErrorType.NotFound);
        patient.FirstName = dto.FirstName.Trim();
        patient.LastName = dto.LastName.Trim();
        patient.DateOfBirth = dto.DateOfBirth!.Value.Date;
        patient.PhoneNumber = dto.PhoneNumber.Trim();
        patient.ProfileIncomplete = false;
        patient.SecurityStamp = Guid.NewGuid(); // the old token still carries the profile flag
        var saved = await SaveAsync(unit, "Konto zmieniło się w międzyczasie.");
        return saved.IsSuccess ? Result.Success(sessions.CreateSession(patient)) : Result.Failure<AuthenticatedPatient>(saved.Error!, saved.ErrorType);
    }

    private static (string First, string Last) Names(ExternalIdentity identity)
    {
        static string Clip(string value) => value.Length > 50 ? value[..50] : value;
        var first = identity.GivenName?.Trim();
        var last = identity.FamilyName?.Trim();
        if (string.IsNullOrEmpty(first) && !string.IsNullOrWhiteSpace(identity.Name))
        {
            var parts = identity.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            first = parts[0];
            last ??= parts.Length > 1 ? parts[1] : null;
        }
        if (string.IsNullOrEmpty(first)) first = identity.Email.Split('@')[0];
        return (Clip(first), Clip(string.IsNullOrEmpty(last) ? "-" : last));
    }

    private static async Task<Result> SaveAsync(IUnitOfWork unit, string conflict)
    {
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException) { return Result.Failure(conflict, ErrorType.Conflict); }
    }
}

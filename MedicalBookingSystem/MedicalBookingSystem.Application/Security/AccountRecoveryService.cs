using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MedicalBookingSystem.Application.Notifications;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Application.Security;

/// <summary>
/// Email confirmation and password reset for patients. Links carry a random one-time token; the
/// database keeps only its hash. Requests for unknown or demo addresses look exactly like real ones.
/// </summary>
public sealed class AccountRecoveryService(IUnitOfWorkFactory units, IPasswordHasher<Patient> hasher, ClinicClock clock,
    UserAccess access, IOptions<NotificationOptions> options, IValidator<ForgotPasswordDto> forgotValidator,
    IValidator<ResetPasswordDto> resetValidator)
{
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromDays(3);
    public static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(2);
    public const string InvalidLink = "Link jest nieprawidłowy, wygasł albo został już użyty. Poproś o nowy.";

    /// <summary>Queues a confirmation email in the caller's unit of work, saved together with the account.</summary>
    public async Task QueueConfirmationAsync(IUnitOfWork unit, Patient patient)
    {
        var now = clock.UtcNow;
        var token = NewToken(patient, AccountTokenPurpose.EmailConfirmation, now, ConfirmationLifetime);
        await unit.AccountTokens.AddAsync(token.Entity);
        await unit.EmailOutbox.AddAsync(EmailTemplates.Create(patient.Email, "Potwierdź adres email",
            $"Witaj, {patient.FirstName}!", "Dziękujemy za rejestrację. Potwierdź, że ten adres email należy do Ciebie.",
            "Potwierdź email", options.Value.Link($"/confirm-email?token={token.Raw}"),
            $"Link jest ważny przez {ConfirmationLifetime.TotalDays:0} dni. Jeśli to nie Ty zakładałeś konto, zignoruj tę wiadomość.", now));
    }

    public async Task<Result> RequestPasswordResetAsync(ForgotPasswordDto dto)
    {
        var validation = await forgotValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var found = await unit.Patients.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        if (found is null || found.IsDemo) return Result.Success();
        var now = clock.UtcNow;
        await InvalidateAsync(unit, found.Id, AccountTokenPurpose.PasswordReset, now);
        var patient = (await unit.Patients.GetByIdAsync(found.Id))!;
        var token = NewToken(patient, AccountTokenPurpose.PasswordReset, now, ResetLifetime);
        await unit.AccountTokens.AddAsync(token.Entity);
        await unit.EmailOutbox.AddAsync(EmailTemplates.Create(patient.Email, "Reset hasła",
            "Ustaw nowe hasło", "Otrzymaliśmy prośbę o zmianę hasła do Twojego konta pacjenta.",
            "Ustaw nowe hasło", options.Value.Link($"/reset-password?token={token.Raw}"),
            $"Link jest jednorazowy i ważny przez {ResetLifetime.TotalMinutes:0} minut. Jeśli to nie Ty, zignoruj wiadomość — hasło się nie zmieni.", now));
        await unit.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var validation = await resetValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create();
        var (token, patient) = await RedeemAsync(unit, dto.Token, AccountTokenPurpose.PasswordReset);
        if (token is null || patient is null) return Result.Failure(InvalidLink);
        if (patient.IsDemo) return DemoAccounts.Refused();
        patient.PasswordHash = hasher.HashPassword(patient, dto.NewPassword);
        patient.SecurityStamp = Guid.NewGuid();
        patient.EmailConfirmed = true; // following the link proves access to the mailbox
        patient.FailedLoginCount = 0;
        patient.LockoutEndUtc = null;
        return await SaveAsync(unit);
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) || dto.Token.Length > 100) return Result.Failure(InvalidLink);
        using var unit = units.Create();
        var (token, patient) = await RedeemAsync(unit, dto.Token, AccountTokenPurpose.EmailConfirmation);
        if (token is null || patient is null) return Result.Failure(InvalidLink);
        patient.EmailConfirmed = true;
        return await SaveAsync(unit);
    }

    public async Task<Result> ResendConfirmationAsync(int patientId)
    {
        await access.RequirePatientAsync(patientId);
        using var unit = units.Create();
        var patient = await unit.Patients.GetByIdAsync(patientId);
        if (patient is null) return Result.Failure("Pacjent nie został znaleziony.", ErrorType.NotFound);
        if (patient.EmailConfirmed) return Result.Success();
        var now = clock.UtcNow;
        var recent = now - ResendInterval;
        if (await unit.AccountTokens.AnyAsync(t => t.PatientId == patientId && t.Purpose == AccountTokenPurpose.EmailConfirmation && t.CreatedAtUtc > recent))
            return Result.Failure($"Wiadomość została wysłana przed chwilą. Spróbuj ponownie za {ResendInterval.TotalMinutes:0} minuty.", ErrorType.Conflict);
        await InvalidateAsync(unit, patientId, AccountTokenPurpose.EmailConfirmation, now);
        await QueueConfirmationAsync(unit, patient);
        return await SaveAsync(unit);
    }

    private async Task<(AccountToken? Token, Patient? Patient)> RedeemAsync(IUnitOfWork unit, string raw, AccountTokenPurpose purpose)
    {
        var hash = Hash(raw.Trim());
        var found = (await unit.AccountTokens.FindAsync(t => t.TokenHash == hash)).SingleOrDefault();
        var now = clock.UtcNow;
        if (found is null || found.Purpose != purpose || found.UsedAtUtc is not null || found.ExpiresAtUtc <= now) return (null, null);
        var token = await unit.AccountTokens.GetByIdAsync(found.Id);
        var patient = await unit.Patients.GetByIdAsync(found.PatientId);
        if (token is null || patient is null) return (null, null);
        token.UsedAtUtc = now;
        return (token, patient);
    }

    private static async Task InvalidateAsync(IUnitOfWork unit, int patientId, AccountTokenPurpose purpose, DateTime now)
    {
        foreach (var open in await unit.AccountTokens.FindAsync(t => t.PatientId == patientId && t.Purpose == purpose && t.UsedAtUtc == null))
            if (await unit.AccountTokens.GetByIdAsync(open.Id) is { } tracked) tracked.UsedAtUtc = now;
    }

    private static async Task<Result> SaveAsync(IUnitOfWork unit)
    {
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException) { return Result.Failure("Konto zmieniło się w międzyczasie. Spróbuj ponownie.", ErrorType.Conflict); }
    }

    private static (AccountToken Entity, string Raw) NewToken(Patient patient, AccountTokenPurpose purpose, DateTime now, TimeSpan lifetime)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (new AccountToken
        {
            Patient = patient, PatientId = patient.Id, Purpose = purpose, TokenHash = Hash(raw),
            CreatedAtUtc = now, ExpiresAtUtc = now + lifetime
        }, raw);
    }

    public static string Hash(string raw) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}

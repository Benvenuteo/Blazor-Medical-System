using System.Security.Claims;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Security;

public sealed class StaffAuthenticationService(IUnitOfWorkFactory units, IPasswordHasher<StaffAccount> hasher, ClinicClock clock)
{
    public async Task<ClaimsPrincipal?> AuthenticateAsync(LoginDto dto)
    {
        using var unitOfWork = units.Create();
        var email = dto.Email.Trim().ToLowerInvariant();
        var account = (await unitOfWork.StaffAccounts.FindAsync(a => a.Email == email)).SingleOrDefault();
        var now = clock.UtcNow;
        // Every path computes exactly one password hash, so response time does not reveal whether the email exists.
        if (account is null || !account.IsActive || (account.Role == StaffRole.Doctor && account.DoctorId is null) ||
            LoginThrottle.IsLockedOut(account.LockoutEndUtc, account.IsDemo, now))
        {
            hasher.VerifyHashedPassword(TimingDummy, DummyHash(), dto.Password);
            return null;
        }
        var failed = hasher.VerifyHashedPassword(account, account.PasswordHash, dto.Password) == PasswordVerificationResult.Failed;
        if (failed || account.FailedLoginCount != 0 || account.LockoutEndUtc is not null)
        {
            // The email lookup is read-only; counters are updated on a tracked copy of the account.
            var tracked = await unitOfWork.StaffAccounts.GetByIdAsync(account.Id);
            if (tracked is not null)
            {
                (tracked.FailedLoginCount, tracked.LockoutEndUtc) = failed
                    ? LoginThrottle.RegisterFailure(tracked.FailedLoginCount, tracked.IsDemo, now) : (0, null);
                await SaveCountersAsync(unitOfWork);
            }
        }
        if (failed) return null;
        return AccountClaims.Create(account.Id, account.DisplayName, account.Email,
            account.Role == StaffRole.Admin ? Roles.Admin : Roles.Doctor, new DateTimeOffset(clock.UtcNow).AddHours(1), account.DoctorId, account.SecurityStamp, account.MustChangePassword, account.IsDemo);
    }

    private static readonly StaffAccount TimingDummy = new();
    private static string? dummyHash;
    private string DummyHash() => dummyHash ??= hasher.HashPassword(TimingDummy, Guid.NewGuid().ToString("N"));

    private static async Task SaveCountersAsync(IUnitOfWork unit)
    {
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException) { }
    }
}

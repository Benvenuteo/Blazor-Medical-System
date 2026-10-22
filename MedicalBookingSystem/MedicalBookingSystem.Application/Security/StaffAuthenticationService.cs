using System.Security.Claims;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
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
        if (account is null || !account.IsActive ||
            (account.Role == StaffRole.Doctor && account.DoctorId is null) ||
            hasher.VerifyHashedPassword(account, account.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return null;
        return AccountClaims.Create(account.Id, account.DisplayName, account.Email,
            account.Role == StaffRole.Admin ? Roles.Admin : Roles.Doctor, new DateTimeOffset(clock.UtcNow).AddHours(1), account.DoctorId, account.SecurityStamp, account.MustChangePassword);
    }
}

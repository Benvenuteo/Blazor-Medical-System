using System.Security.Claims;
using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Security;

/// <summary>The principal comes from the authenticated host (HTTP or circuit), never from a request body.</summary>
public sealed class StaffPasswordService(IUnitOfWorkFactory units, SessionGuard guard, IPasswordHasher<StaffAccount> hasher,
    IValidator<ChangeStaffPasswordDto> validator)
{
    public async Task<Result> ChangeAsync(ClaimsPrincipal principal, ChangeStaffPasswordDto dto)
    {
        await guard.RequireAsync(principal, allowPasswordChange: true);
        if (!AccountClaims.TryReadIdentity(principal, out var identity) || !identity.IsStaff) throw new AccessDeniedException(true);
        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        using var unit = units.Create(); var account = await unit.StaffAccounts.GetByIdAsync(identity.Id);
        if (account is null || !account.IsActive || account.SecurityStamp != identity.SecurityStamp) throw new AccessDeniedException(false);
        if (hasher.VerifyHashedPassword(account, account.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result.Failure("Aktualne hasło jest nieprawidłowe.");
        if (string.Equals(dto.CurrentPassword, dto.NewPassword, StringComparison.Ordinal)) return Result.Failure("Nowe hasło musi różnić się od aktualnego.");
        account.PasswordHash = hasher.HashPassword(account, dto.NewPassword);
        account.SecurityStamp = Guid.NewGuid(); account.MustChangePassword = false;
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException) { return Result.Failure("Konto zmieniło się. Zaloguj się ponownie i spróbuj jeszcze raz.", ErrorType.Conflict); }
    }
}

using System.Security.Claims;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Security;

public sealed class SessionManagementService(IUnitOfWorkFactory units, SessionGuard guard)
{
    public async Task<Result> RevokeAllAsync(ClaimsPrincipal principal)
    {
        await guard.RequireAsync(principal, allowPasswordChange: true);
        if (!AccountClaims.TryReadIdentity(principal, out var identity)) throw new AccessDeniedException(false);
        using var unit = units.Create();
        if (identity.Role == Roles.Patient)
        {
            var patient = await unit.Patients.GetByIdAsync(identity.Id);
            if (patient is null || patient.SecurityStamp != identity.SecurityStamp) throw new AccessDeniedException(false);
            patient.SecurityStamp = Guid.NewGuid();
        }
        else
        {
            var staff = await unit.StaffAccounts.GetByIdAsync(identity.Id);
            if (staff is null || !staff.IsActive || staff.SecurityStamp != identity.SecurityStamp) throw new AccessDeniedException(false);
            staff.SecurityStamp = Guid.NewGuid();
        }
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException) { return Result.Failure("Konto zmieniło się. Zaloguj się ponownie.", ErrorType.Conflict); }
    }
}

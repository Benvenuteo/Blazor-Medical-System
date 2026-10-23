using System.Security.Claims;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Security;

/// <summary>No circuit-wide cache: account changes must take effect on the next protected operation.</summary>
public sealed class AccountSessionValidator(IUnitOfWorkFactory units, ClinicClock clock) : ISessionValidator
{
    public async Task<SessionStatus> ValidateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (!SessionGuard.IsUnexpired(user, clock) || !AccountClaims.TryReadIdentity(user, out var identity)) return SessionStatus.Invalid;
        cancellationToken.ThrowIfCancellationRequested();
        using var unit = units.Create();
        if (identity.Role == Roles.Patient)
        {
            var patient = await unit.Patients.GetByIdAsync(identity.Id);
            cancellationToken.ThrowIfCancellationRequested();
            return patient?.SecurityStamp == identity.SecurityStamp ? SessionStatus.Valid : SessionStatus.Invalid;
        }
        var staff = await unit.StaffAccounts.GetByIdAsync(identity.Id);
        cancellationToken.ThrowIfCancellationRequested();
        if (staff is null || !staff.IsActive || staff.SecurityStamp != identity.SecurityStamp ||
            (staff.Role == StaffRole.Admin ? Roles.Admin : Roles.Doctor) != identity.Role ||
            staff.DoctorId != identity.DoctorId || staff.MustChangePassword != identity.MustChangePassword) return SessionStatus.Invalid;
        return staff.MustChangePassword ? SessionStatus.PasswordChangeRequired : SessionStatus.Valid;
    }
}

using System.Globalization;
using System.Security.Claims;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Security;

/// <summary>Enforces permissions at the application boundary, including direct calls from a server circuit.</summary>
public sealed class UserAccess(ICurrentUser currentUser, SessionGuard sessions)
{
    private async Task<ClaimsPrincipal> GetUserAsync(bool allowIncompleteProfile = false) =>
        await sessions.RequireAsync(await currentUser.GetPrincipalAsync(), allowIncompleteProfile: allowIncompleteProfile);

    public async Task<AccountIdentity> IdentityAsync()
    {
        var user = await GetUserAsync();
        if (!AccountClaims.TryReadIdentity(user, out var identity)) throw new AccessDeniedException(false);
        return identity;
    }

    public async Task<int> StaffAccountIdAsync()
    {
        var user = await GetUserAsync();
        if (!AccountClaims.TryReadIdentity(user, out var identity) || !identity.IsStaff) throw new AccessDeniedException(true);
        return identity.Id;
    }

    public async Task<int?> PatientIdOrNullAsync()
    {
        try
        {
            var user = await GetUserAsync();
            return user.IsInRole(Roles.Patient) ? AccountClaims.ReadId(user, AccountClaims.PatientId) : null;
        }
        catch (AccessDeniedException) { return null; }
    }

    /// <param name="allowIncompleteProfile">True only for reading/completing the profile and ending sessions.</param>
    public async Task<int> PatientIdAsync(bool allowIncompleteProfile = false)
    {
        var user = await GetUserAsync(allowIncompleteProfile);
        if (!user.IsInRole(Roles.Patient) || AccountClaims.ReadId(user, AccountClaims.PatientId) is not int id)
            throw new AccessDeniedException(true);
        return id;
    }

    public async Task<int> DoctorIdAsync()
    {
        var user = await GetUserAsync();
        if (!user.IsInRole(Roles.Doctor) || AccountClaims.ReadId(user, AccountClaims.DoctorId) is not int id)
            throw new AccessDeniedException(true);
        return id;
    }

    public async Task RequirePatientAsync(int patientId, bool allowIncompleteProfile = false)
    {
        if (await PatientIdAsync(allowIncompleteProfile) != patientId) throw new AccessDeniedException(true);
    }

    public async Task RequireDoctorAsync(int doctorId)
    {
        if (await DoctorIdAsync() != doctorId) throw new AccessDeniedException(true);
    }

    public async Task RequireAdminAsync()
    {
        if (!(await GetUserAsync()).IsInRole(Roles.Admin)) throw new AccessDeniedException(true);
    }

    public async Task RequireScheduleManagementAsync(int doctorId)
    {
        var user = await GetUserAsync();
        if (!user.IsInRole(Roles.Admin) &&
            !(user.IsInRole(Roles.Doctor) && AccountClaims.ReadId(user, AccountClaims.DoctorId) == doctorId))
            throw new AccessDeniedException(true);
    }

    public async Task RequireMedicalRecordAsync(int patientId, int doctorId)
    {
        var user = await GetUserAsync();
        if (!(user.IsInRole(Roles.Patient) && AccountClaims.ReadId(user, AccountClaims.PatientId) == patientId) &&
            !(user.IsInRole(Roles.Doctor) && AccountClaims.ReadId(user, AccountClaims.DoctorId) == doctorId))
            throw new AccessDeniedException(true);
    }
}

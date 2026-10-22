using System.Globalization;
using System.Security.Claims;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Security;

/// <summary>Enforces permissions at the application boundary, including direct calls from a server circuit.</summary>
public sealed class UserAccess(ICurrentUser currentUser, SessionGuard sessions)
{
    private async Task<ClaimsPrincipal> GetUserAsync() => await sessions.RequireAsync(await currentUser.GetPrincipalAsync());

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

    public async Task<int> PatientIdAsync()
    {
        var user = await GetUserAsync();
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

    public async Task RequirePatientAsync(int patientId)
    {
        if (await PatientIdAsync() != patientId) throw new AccessDeniedException(true);
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

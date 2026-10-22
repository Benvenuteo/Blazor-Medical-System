using System.Globalization;
using System.Security.Claims;

namespace MedicalBookingSystem.SharedKernel.Security;

public static class Roles
{
    public const string Patient = "Patient";
    public const string Doctor = "Doctor";
    public const string Admin = "Admin";
    public const string Staff = Doctor + "," + Admin;
}

public static class AccountClaims
{
    public const string SecurityStamp = "security_stamp";
    public const string PasswordChangeRequired = "password_change_required";
    public const string PatientId = "patient_id";
    public const string DoctorId = "doctor_id";
    public const string ExpiresAt = "session_expires";

    public static int? ReadId(ClaimsPrincipal user, string claimType) =>
        int.TryParse(user.FindFirst(claimType)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    public static ClaimsPrincipal Create(int accountId, string name, string email, string role,
        DateTimeOffset expires, int? doctorId = null, Guid? securityStamp = null, bool mustChangePassword = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"{(role == Roles.Patient ? "patient" : "staff")}:{accountId}"),
            new(ClaimTypes.Name, name), new(ClaimTypes.Email, email), new(ClaimTypes.Role, role),
            new(ExpiresAt, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new(SecurityStamp, (securityStamp ?? Guid.Empty).ToString("N"))
        };
        if (role == Roles.Patient)
            claims.Add(new(PatientId, accountId.ToString(CultureInfo.InvariantCulture)));
        if (role == Roles.Doctor && doctorId.HasValue)
            claims.Add(new(DoctorId, doctorId.Value.ToString(CultureInfo.InvariantCulture)));
        if (mustChangePassword) claims.Add(new(PasswordChangeRequired, "true"));
        return new(new ClaimsIdentity(claims, "MedicalAccount"));
    }
    /// <summary>Read exactly one account identity. Signed tokens still need the database session check.</summary>
    public static bool TryReadIdentity(ClaimsPrincipal user, out AccountIdentity identity)
    {
        identity = default;
        var subjects = user.FindAll(ClaimTypes.NameIdentifier).ToArray();
        var roles = user.FindAll(ClaimTypes.Role).ToArray();
        var stamps = user.FindAll(SecurityStamp).ToArray();
        var passwordFlags = user.FindAll(PasswordChangeRequired).ToArray();
        if (subjects.Length != 1 || roles.Length != 1 || stamps.Length != 1 || passwordFlags.Length > 1 ||
            !Guid.TryParseExact(stamps[0].Value, "N", out var stamp) || stamp == Guid.Empty ||
            (passwordFlags.Length == 1 && passwordFlags[0].Value != "true")) return false;
        var role = roles[0].Value;
        if (role is not (Roles.Patient or Roles.Doctor or Roles.Admin)) return false;
        var prefix = role == Roles.Patient ? "patient:" : "staff:";
        if (!subjects[0].Value.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(subjects[0].Value[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) return false;
        var patients = user.FindAll(PatientId).ToArray();
        var doctors = user.FindAll(DoctorId).ToArray();
        if (role == Roles.Patient ? patients.Length != 1 || ReadId(user, PatientId) != id : patients.Length != 0) return false;
        if (role == Roles.Doctor ? doctors.Length != 1 || ReadId(user, DoctorId) is null : doctors.Length != 0) return false;
        if (role == Roles.Patient && passwordFlags.Length != 0) return false;
        identity = new AccountIdentity(id, role, stamp, role == Roles.Doctor ? ReadId(user, DoctorId) : null, passwordFlags.Length == 1);
        return true;
    }

}


public readonly record struct AccountIdentity(int Id, string Role, Guid SecurityStamp, int? DoctorId, bool MustChangePassword)
{
    public bool IsStaff => Role is Roles.Doctor or Roles.Admin;
}

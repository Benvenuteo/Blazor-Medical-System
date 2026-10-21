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
    public const string PatientId = "patient_id";
    public const string DoctorId = "doctor_id";
    public const string ExpiresAt = "session_expires";

    public static int? ReadId(ClaimsPrincipal user, string claimType) =>
        int.TryParse(user.FindFirst(claimType)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    public static ClaimsPrincipal Create(int accountId, string name, string email, string role,
        DateTimeOffset expires, int? doctorId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"{(role == Roles.Patient ? "patient" : "staff")}:{accountId}"),
            new(ClaimTypes.Name, name), new(ClaimTypes.Email, email), new(ClaimTypes.Role, role),
            new(ExpiresAt, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };
        if (role == Roles.Patient)
            claims.Add(new(PatientId, accountId.ToString(CultureInfo.InvariantCulture)));
        if (role == Roles.Doctor && doctorId.HasValue)
            claims.Add(new(DoctorId, doctorId.Value.ToString(CultureInfo.InvariantCulture)));
        return new(new ClaimsIdentity(claims, "MedicalAccount"));
    }
}

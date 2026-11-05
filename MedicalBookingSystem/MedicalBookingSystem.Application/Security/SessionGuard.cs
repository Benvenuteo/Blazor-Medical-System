using System.Globalization;
using System.Security.Claims;
using MedicalBookingSystem.Application.Time;

namespace MedicalBookingSystem.Application.Security;

public sealed class SessionGuard(ClinicClock clock, ISessionValidator sessions)
{
    public static bool IsUnexpired(ClaimsPrincipal user, ClinicClock clock) => user.Identity?.IsAuthenticated == true &&
        user.FindAll(SharedKernel.Security.AccountClaims.ExpiresAt).Count() == 1 &&
        long.TryParse(user.FindFirst(SharedKernel.Security.AccountClaims.ExpiresAt)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var expires) &&
        expires > new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds();

    public const string ProfileIncompleteMessage = "Uzupełnij profil (data urodzenia i telefon) przed wykonaniem tej operacji.";

    /// <param name="allowIncompleteProfile">Only profile, completion and logout operations pass this; the state comes from the database.</param>
    public async Task<ClaimsPrincipal> RequireAsync(ClaimsPrincipal user, bool allowPasswordChange = false, bool allowIncompleteProfile = false)
    {
        if (!IsUnexpired(user, clock)) throw new AccessDeniedException(false);
        var status = await sessions.ValidateAsync(user);
        if (status == SessionStatus.Invalid) throw new AccessDeniedException(false);
        if (status == SessionStatus.PasswordChangeRequired && !allowPasswordChange)
            throw new AccessDeniedException(true, "Zmień hasło tymczasowe przed wykonaniem tej operacji.");
        if (status == SessionStatus.ProfileIncomplete && !allowIncompleteProfile)
            throw new AccessDeniedException(true, ProfileIncompleteMessage);
        return user;
    }
}

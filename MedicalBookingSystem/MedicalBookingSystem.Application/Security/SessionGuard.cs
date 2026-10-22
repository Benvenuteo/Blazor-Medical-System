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

    public async Task<ClaimsPrincipal> RequireAsync(ClaimsPrincipal user, bool allowPasswordChange = false)
    {
        if (!IsUnexpired(user, clock)) throw new AccessDeniedException(false);
        var status = await sessions.ValidateAsync(user);
        if (status == SessionStatus.Invalid) throw new AccessDeniedException(false);
        if (status == SessionStatus.PasswordChangeRequired && !allowPasswordChange)
            throw new AccessDeniedException(true, "Zmień hasło tymczasowe przed wykonaniem tej operacji.");
        return user;
    }
}

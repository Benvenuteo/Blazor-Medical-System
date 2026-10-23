using System.Security.Claims;

namespace MedicalBookingSystem.Application.Security;

public enum SessionStatus { Invalid, Valid, PasswordChangeRequired }

public interface ISessionValidator
{
    Task<SessionStatus> ValidateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

using System.Security.Claims;

namespace MedicalBookingSystem.Application.Security;

/// <summary>Uses the HTTP principal in the API and the circuit authentication state in Blazor Server.</summary>
public interface ICurrentUser
{
    Task<ClaimsPrincipal> GetPrincipalAsync();
}

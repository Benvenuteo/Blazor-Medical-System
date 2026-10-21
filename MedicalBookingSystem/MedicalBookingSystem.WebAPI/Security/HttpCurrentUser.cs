using System.Security.Claims;
using MedicalBookingSystem.Application.Security;

namespace MedicalBookingSystem.WebAPI.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Task<ClaimsPrincipal> GetPrincipalAsync() => Task.FromResult(
        accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity()));
}

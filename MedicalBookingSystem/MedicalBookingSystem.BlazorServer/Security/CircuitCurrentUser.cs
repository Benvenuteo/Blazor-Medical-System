using System.Security.Claims;
using MedicalBookingSystem.Application.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace MedicalBookingSystem.BlazorServer.Security;

public sealed class CircuitCurrentUser(AuthenticationStateProvider provider) : ICurrentUser
{
    public async Task<ClaimsPrincipal> GetPrincipalAsync() => (await provider.GetAuthenticationStateAsync()).User;
}

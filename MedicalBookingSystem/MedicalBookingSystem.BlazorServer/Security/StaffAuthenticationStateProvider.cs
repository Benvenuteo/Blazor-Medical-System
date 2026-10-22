using MedicalBookingSystem.Application.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace MedicalBookingSystem.BlazorServer.Security;

public sealed class StaffAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopes)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionValidator>()
            .ValidateAsync(authenticationState.User, cancellationToken) != SessionStatus.Invalid;
    }
}

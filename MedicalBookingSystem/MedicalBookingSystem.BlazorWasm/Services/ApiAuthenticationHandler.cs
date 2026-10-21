using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace MedicalBookingSystem.BlazorWasm.Services;

public sealed class ApiAuthenticationHandler(
    PatientAuthenticationStateProvider authentication, NavigationManager navigation, Uri apiAddress) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sameOrigin = request.RequestUri is { } uri && uri.Scheme == apiAddress.Scheme &&
            uri.Authority == apiAddress.Authority;
        if (sameOrigin)
        {
            var token = await authentication.GetTokenAsync();
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var response = await base.SendAsync(request, cancellationToken);
        if (sameOrigin && response.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization is not null)
        {
            await authentication.LogoutAsync();
            navigation.NavigateTo("/", replace: true);
        }
        return response;
    }
}

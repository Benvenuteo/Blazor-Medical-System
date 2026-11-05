using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class SessionRenewalTests
{
    [Fact]
    public async Task Refresh_IssuesALaterToken_AndKeepsTheOriginalSignInTime()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(testClock: clock));
        using var client = await scenario.SignInAsync();
        var first = ReadToken(client);

        clock.SetUtcNow(clock.GetUtcNow().AddMinutes(50));
        using var response = await client.PostAsync("/api/auth/refresh", null);
        var renewed = (await response.Content.ReadFromJsonAsync<AuthenticationResponseDto>())!.Token;
        var second = new JwtSecurityTokenHandler().ReadJwtToken(renewed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(second.ValidTo > first.ValidTo);
        Assert.Equal(first.Claims.Single(c => c.Type == AccountClaims.SessionStarted).Value,
            second.Claims.Single(c => c.Type == AccountClaims.SessionStarted).Value);
    }

    [Fact]
    public async Task Renewal_StopsAfterTwelveHoursFromSignIn()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(testClock: clock));
        using var client = await scenario.SignInAsync();
        var renewals = 0;
        for (var step = 0; step < 20; step++)
        {
            clock.SetUtcNow(clock.GetUtcNow().AddMinutes(50));
            using var response = await client.PostAsync("/api/auth/refresh", null);
            if (!response.IsSuccessStatusCode) break;
            var token = (await response.Content.ReadFromJsonAsync<AuthenticationResponseDto>())!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            renewals++;
        }

        // 14 × 50 min = 11 h 40 min; the next attempt is past the 12-hour limit (the last token ends exactly at 12 h).
        Assert.Equal(14, renewals);
    }

    [Fact]
    public async Task Refresh_IsRefusedAfterLogoutEverywhere()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var other = await scenario.SignInAsync();

        using var logout = await other.PostAsync("/api/auth/logout-all", null);
        using var refresh = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    private static JwtSecurityToken ReadToken(HttpClient client) =>
        new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter);
}

using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class LoginLockoutTests
{
    [Theory]
    [InlineData("/api/auth/login", "one@example.test")]
    [InlineData("/api/auth/staff/login", "admin@example.test")]
    public async Task FiveWrongPasswords_LockTheAccount_UntilTheLockoutEnds(string path, string email)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        for (var attempt = 0; attempt < LoginThrottle.MaxFailures; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, path, email, "Wrong2026!")).StatusCode);

        using var locked = await LoginAsync(client, path, email, SecurityScenario.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("blokowane", await locked.Content.ReadAsStringAsync());

        await ExpireLockoutAsync(scenario);
        using var afterLockout = await LoginAsync(client, path, email, SecurityScenario.Password);
        Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsTheFailureCounter()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        for (var attempt = 0; attempt < LoginThrottle.MaxFailures - 1; attempt++)
            await LoginAsync(client, "/api/auth/login", "one@example.test", "Wrong2026!");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "/api/auth/login", "one@example.test", SecurityScenario.Password)).StatusCode);

        for (var attempt = 0; attempt < LoginThrottle.MaxFailures - 1; attempt++)
            await LoginAsync(client, "/api/auth/login", "one@example.test", "Wrong2026!");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "/api/auth/login", "one@example.test", SecurityScenario.Password)).StatusCode);
    }

    [Fact]
    public async Task DemoAccount_IsNeverLocked_SoVisitorsCannotBlockThePublicDemo()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Patients
                .Where(p => p.Email == "one@example.test").ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDemo, true));
        using var client = scenario.Factory.CreateClient();
        for (var attempt = 0; attempt < LoginThrottle.MaxFailures + 2; attempt++)
            await LoginAsync(client, "/api/auth/login", "one@example.test", "Wrong2026!");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "/api/auth/login", "one@example.test", SecurityScenario.Password)).StatusCode);
    }

    [Fact]
    public async Task UnknownEmail_AndWrongPassword_ReturnTheSameMessage()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();

        using var unknown = await LoginAsync(client, "/api/auth/login", "nobody@example.test", "Wrong2026!");
        using var wrong = await LoginAsync(client, "/api/auth/login", "one@example.test", "Wrong2026!");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains(LoginThrottle.FailureMessage, await unknown.Content.ReadAsStringAsync());
        Assert.Contains(LoginThrottle.FailureMessage, await wrong.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string path, string email, string password) =>
        client.PostAsJsonAsync(path, new LoginDto { Email = email, Password = password });

    private static async Task ExpireLockoutAsync(SecurityScenario scenario)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var past = DateTime.UtcNow.AddMinutes(-1);
        await db.Patients.Where(p => p.LockoutEndUtc != null).ExecuteUpdateAsync(s => s.SetProperty(p => p.LockoutEndUtc, past));
        await db.StaffAccounts.Where(a => a.LockoutEndUtc != null).ExecuteUpdateAsync(s => s.SetProperty(a => a.LockoutEndUtc, past));
    }
}

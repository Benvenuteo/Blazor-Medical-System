using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.Infrastructure.Email;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AccountRecoveryTests
{
    [Fact]
    public async Task Registration_QueuesConfirmation_AndTheLinkConfirmsTheEmailOnce()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterPatientDto
        {
            Email = "new@example.test", Password = "NewPatient2026!", FirstName = "Nowy", LastName = "Pacjent", DateOfBirth = new DateTime(1990, 5, 5)
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        Assert.False(auth!.Patient.EmailConfirmed);

        var token = await LatestTokenAsync(scenario, "new@example.test", "/confirm-email");
        using var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailDto { Token = token });
        using var reuse = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailDto { Token = token });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var profile = await client.GetFromJsonAsync<PatientDto>("/api/patients/profile");

        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.True(profile!.EmailConfirmed);
    }

    [Fact]
    public async Task PasswordReset_ChangesPassword_RevokesSessions_AndTheLinkWorksOnce()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var oldSession = await scenario.SignInAsync();
        using var client = scenario.Factory.CreateClient();

        using var request = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "ONE@example.test" });
        var token = await LatestTokenAsync(scenario, "one@example.test", "/reset-password");
        var reset = new ResetPasswordDto { Token = token, NewPassword = "Recovered2026!", ConfirmNewPassword = "Recovered2026!" };
        using var first = await client.PostAsJsonAsync("/api/auth/reset-password", reset);
        using var second = await client.PostAsJsonAsync("/api/auth/reset-password", reset);

        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/patients/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "one@example.test", Password = SecurityScenario.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "one@example.test", Password = "Recovered2026!" })).StatusCode);
    }

    [Fact]
    public async Task NewResetRequest_InvalidatesTheEarlierLink_AndExpiredLinksAreRejected()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "one@example.test" });
        var earlier = await LatestTokenAsync(scenario, "one@example.test", "/reset-password");
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "one@example.test" });
        var latest = await LatestTokenAsync(scenario, "one@example.test", "/reset-password");
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountTokens
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        using var withEarlier = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordDto { Token = earlier, NewPassword = "Recovered2026!", ConfirmNewPassword = "Recovered2026!" });
        using var withExpired = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordDto { Token = latest, NewPassword = "Recovered2026!", ConfirmNewPassword = "Recovered2026!" });

        Assert.NotEqual(earlier, latest);
        Assert.Equal(HttpStatusCode.BadRequest, withEarlier.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withExpired.StatusCode);
    }

    [Fact]
    public async Task UnknownAndDemoAddresses_GetTheSameAnswer_ButNoEmail()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Patients
                .Where(p => p.Email == "one@example.test").ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDemo, true));
        using var client = scenario.Factory.CreateClient();

        using var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "nobody@example.test" });
        using var demo = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "one@example.test" });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, demo.StatusCode);
        await using var check = scenario.Factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailOutbox.AnyAsync());
    }

    [Fact]
    public async Task ResendConfirmation_IsThrottled()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();

        using var first = await client.PostAsync("/api/auth/resend-confirmation", null);
        using var second = await client.PostAsync("/api/auth/resend-confirmation", null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PickupDelivery_WritesEmlFiles_AndMarksMessagesSent()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "one@example.test" });
        var directory = Path.Combine(Path.GetTempPath(), "medical-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new EmailDeliveryOptions { Mode = EmailDeliveryMode.Pickup, PickupDirectory = directory });
            var transport = new SmtpEmailTransport(options, scenario.Factory.Services.GetRequiredService<IWebHostEnvironment>());
            var delivery = new EmailDeliveryService(scenario.Factory.Services.GetRequiredService<IServiceScopeFactory>(), transport,
                options, TimeProvider.System, NullLogger<EmailDeliveryService>.Instance);

            Assert.Equal(1, await delivery.DeliverPendingAsync(CancellationToken.None));
            Assert.Equal(0, await delivery.DeliverPendingAsync(CancellationToken.None));
            var file = Assert.Single(Directory.GetFiles(directory, "*.eml"));
            Assert.Contains("one@example.test", await File.ReadAllTextAsync(file));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static async Task<string> LatestTokenAsync(SecurityScenario scenario, string email, string path)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var message = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailOutbox.AsNoTracking()
            .Where(m => m.To == email).OrderByDescending(m => m.Id).FirstAsync();
        var match = Regex.Match(message.TextBody, Regex.Escape(path) + @"\?token=([A-Za-z0-9_-]+)");
        Assert.True(match.Success, "The email must contain a link with a token.");
        return match.Groups[1].Value;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class GoogleSignInTests
{
    [Fact]
    public async Task NewGoogleUser_MustCompleteProfile_BeforeBooking()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        var (token, nonce) = scenario.Factory.ExternalTokens.Issue("google-sub-1", "Gosia@Gmail.example");

        using var signIn = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = token, Nonce = nonce });
        var auth = await signIn.Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        using var blocked = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate });
        using var profile = await client.GetAsync("/api/patients/profile");
        using var tooYoung = await client.PutAsJsonAsync("/api/auth/profile/complete", new CompleteProfileDto
            { FirstName = "Gosia", LastName = "Google", DateOfBirth = DateTime.Today.AddYears(-10), PhoneNumber = "600700800" });
        using var complete = await client.PutAsJsonAsync("/api/auth/profile/complete", new CompleteProfileDto
            { FirstName = "Małgorzata", LastName = "Google", DateOfBirth = new DateTime(1992, 3, 4), PhoneNumber = "600700800" });
        var completed = await complete.Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        using var stale = await client.GetAsync("/api/patients/profile");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", completed!.Token);
        using var booked = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.True(auth.Patient.ProfileIncomplete);
        Assert.False(auth.Patient.HasPassword);
        Assert.Equal("gosia@gmail.example", auth.Patient.Email);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("Uzupełnij profil", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooYoung.StatusCode);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.False(completed.Patient.ProfileIncomplete);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
    }

    [Fact]
    public async Task ReturningGoogleUser_GetsTheSameAccount_EvenIfGoogleEmailChanged()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        var first = scenario.Factory.ExternalTokens.Issue("google-sub-2", "before@gmail.example");
        var second = scenario.Factory.ExternalTokens.Issue("google-sub-2", "after@gmail.example");

        var a = await (await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = first.IdToken, Nonce = first.Nonce })).Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        var b = await (await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = second.IdToken, Nonce = second.Nonce })).Content.ReadFromJsonAsync<AuthenticationResponseDto>();

        Assert.Equal(a!.Patient.Id, b!.Patient.Id);
    }

    [Fact]
    public async Task ExistingConfirmedAccount_IsNotLinkedByEmail_AndTheOwnerCanLinkWithPassword()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await SetConfirmedAsync(scenario, "one@example.test");
        using var anonymous = scenario.Factory.CreateClient();
        var attempt = scenario.Factory.ExternalTokens.Issue("google-owner", "one@example.test");

        using var refused = await anonymous.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = attempt.IdToken, Nonce = attempt.Nonce });
        using var owner = await scenario.SignInAsync();
        var link = scenario.Factory.ExternalTokens.Issue("google-owner", "one@example.test");
        using var withoutPassword = await owner.PostAsJsonAsync("/api/auth/google/link", new LinkGoogleDto { IdToken = link.IdToken, Nonce = link.Nonce, CurrentPassword = "Wrong2026!" });
        using var linked = await owner.PostAsJsonAsync("/api/auth/google/link", new LinkGoogleDto { IdToken = link.IdToken, Nonce = link.Nonce, CurrentPassword = SecurityScenario.Password });
        var status = await owner.GetFromJsonAsync<ExternalLoginStatusDto>("/api/auth/google/status");
        var again = scenario.Factory.ExternalTokens.Issue("google-owner", "one@example.test");
        using var googleLogin = await anonymous.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = again.IdToken, Nonce = again.Nonce });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("account_exists_link_required", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, withoutPassword.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        Assert.True(status!.GoogleLinked);
        Assert.Equal(HttpStatusCode.OK, googleLogin.StatusCode);
        Assert.Equal(scenario.PatientId, (await googleLogin.Content.ReadFromJsonAsync<AuthenticationResponseDto>())!.Patient.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync("/api/auth/google/link")).StatusCode);
    }

    [Fact]
    public async Task UnconfirmedRegistration_IsTakenOverByTheVerifiedMailboxOwner()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        var google = scenario.Factory.ExternalTokens.Issue("google-real-owner", "one@example.test");

        using var signIn = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = google.IdToken, Nonce = google.Nonce });
        using var squatterPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "one@example.test", Password = SecurityScenario.Password });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(scenario.PatientId, (await signIn.Content.ReadFromJsonAsync<AuthenticationResponseDto>())!.Patient.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, squatterPassword.StatusCode);
    }

    [Fact]
    public async Task GoogleOnlyAccount_CannotUnlinkOrChangePasswordDirectly_ButCanUsePasswordReset()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        var google = scenario.Factory.ExternalTokens.Issue("google-only", "only@gmail.example");
        var auth = await (await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = google.IdToken, Nonce = google.Nonce }))
            .Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var done = await (await client.PutAsJsonAsync("/api/auth/profile/complete", new CompleteProfileDto
            { FirstName = "Only", LastName = "Google", DateOfBirth = new DateTime(1990, 1, 1), PhoneNumber = "600700800" })).Content.ReadFromJsonAsync<AuthenticationResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", done!.Token);

        using var unlink = await client.DeleteAsync("/api/auth/google/link");
        using var password = await client.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto { NewPassword = "Chosen2026!", ConfirmNewPassword = "Chosen2026!", CurrentPassword = "x" });
        using var passwordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "only@gmail.example", Password = "anything" });
        using var reset = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "only@gmail.example" });

        Assert.Equal(HttpStatusCode.Conflict, unlink.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, password.StatusCode);
        Assert.Contains("Nie pamiętasz hasła", await password.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, passwordLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
    }

    [Fact]
    public async Task InvalidTokens_UnverifiedEmails_DemoAccounts_AndDisabledProvider_AreRejected()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        var valid = scenario.Factory.ExternalTokens.Issue("google-x", "x@gmail.example");
        var unverified = scenario.Factory.ExternalTokens.Issue("google-y", "y@gmail.example", verified: false);
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Patients
                .Where(p => p.Email == "two@example.test").ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDemo, true));
        var demo = scenario.Factory.ExternalTokens.Issue("google-demo", "two@example.test");

        using var wrongNonce = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = valid.IdToken, Nonce = "other" });
        using var notVerified = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = unverified.IdToken, Nonce = unverified.Nonce });
        using var demoAttempt = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = demo.IdToken, Nonce = demo.Nonce });
        scenario.Factory.ExternalTokens.GoogleEnabled = false;
        using var disabled = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInDto { IdToken = valid.IdToken, Nonce = valid.Nonce });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongNonce.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, notVerified.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, demoAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
    }

    private static async Task SetConfirmedAsync(SecurityScenario scenario, string email)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Patients
            .Where(p => p.Email == email).ExecuteUpdateAsync(s => s.SetProperty(p => p.EmailConfirmed, true));
    }
}

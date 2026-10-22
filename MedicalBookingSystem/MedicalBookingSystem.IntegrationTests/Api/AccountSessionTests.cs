using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AccountSessionTests
{
    [Fact]
    public async Task PatientPasswordChange_RevokesAllOldTokens_AndRequiresNewPassword()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var first = await scenario.SignInAsync(); using var second = await scenario.SignInAsync();
        using var other = await scenario.SignInAsync("two@example.test");
        using var change = await first.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto
        { CurrentPassword = SecurityScenario.Password, NewPassword = "ChangedPatient2026!", ConfirmNewPassword = "ChangedPatient2026!" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/patients/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/patients/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/patients/profile")).StatusCode);
        using var anonymous = scenario.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "one@example.test", Password = SecurityScenario.Password })).StatusCode);
        using var fresh = await SignInAsync(scenario, "one@example.test", "ChangedPatient2026!", staff: false);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/patients/profile")).StatusCode);
    }

    [Fact]
    public async Task InvalidPasswordChange_AndDisplayNameUpdate_DoNotRevokeSession()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var client = await scenario.SignInAsync();
        using var rejected = await client.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto
        { CurrentPassword = "Wrong2026!", NewPassword = "ChangedPatient2026!", ConfirmNewPassword = "ChangedPatient2026!" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var update = await client.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto { FirstName = "Updated" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var profile = await client.GetFromJsonAsync<PatientDto>("/api/patients/profile"); Assert.Equal("Updated", profile!.FirstName);
    }

    [Theory]
    [InlineData("one@example.test", false, "/api/patients/profile")]
    [InlineData("admin@example.test", true, "/api/staff/accounts")]
    public async Task LogoutAll_RevokesEveryExistingToken_ButAllowsFreshLogin(string email, bool staff, string protectedPath)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var first = await scenario.SignInAsync(email, staff); using var second = await scenario.SignInAsync(email, staff);
        using var response = await first.PostAsync("/api/auth/logout-all", null); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync(protectedPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync(protectedPath)).StatusCode);
        using var fresh = await scenario.SignInAsync(email, staff); Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync(protectedPath)).StatusCode);
    }

    [Fact]
    public async Task ExistingCircuitService_RejectsRevokedPrincipalOnNextOperation()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var circuit = scenario.Factory.Services.CreateScope();
        var services = circuit.ServiceProvider;
        var principal = await ScenarioPrincipal.StaffAsync(services, "admin@example.test");
        var accountService = ActivatorUtilities.CreateInstance<StaffAccountService>(services, ScenarioPrincipal.Access(services, principal));
        Assert.True((await accountService.SearchAsync(new())).IsSuccess);
        using var client = await scenario.SignInAsync("admin@example.test", true);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout-all", null)).StatusCode);
        var denied = await Assert.ThrowsAsync<AccessDeniedException>(() => accountService.SearchAsync(new()));
        Assert.False(denied.Authenticated);
    }

    [Fact]
    public async Task LoginSnapshot_CannotMintFreshSessionAfterPasswordWasChanged()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var scope = scenario.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var verified = await services.GetRequiredService<PatientAuthenticationService>().LoginAsync(new LoginDto { Email = "one@example.test", Password = SecurityScenario.Password });
        Assert.True(verified.IsSuccess);
        using var current = await scenario.SignInAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await current.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto
        { CurrentPassword = SecurityScenario.Password, NewPassword = "ChangedPatient2026!", ConfirmNewPassword = "ChangedPatient2026!" })).StatusCode);
        Assert.Equal(SessionStatus.Invalid, await services.GetRequiredService<ISessionValidator>().ValidateAsync(verified.Value.Principal));
    }

    [Fact]
    public async Task StoredRoleOrDoctorAssignmentChange_InvalidatesExistingSignedIdentity()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var scope = scenario.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var principal = await ScenarioPrincipal.StaffAsync(services, "doctor@example.test");
        var db = services.GetRequiredService<ApplicationDbContext>();
        var account = await db.StaffAccounts.SingleAsync(a => a.Email == "doctor@example.test");
        account.Role = Domain.Models.StaffRole.Admin; account.DoctorId = null; await db.SaveChangesAsync();
        Assert.Equal(SessionStatus.Invalid, await services.GetRequiredService<ISessionValidator>().ValidateAsync(principal));
    }

    internal static async Task<HttpClient> SignInAsync(SecurityScenario scenario, string email, string password, bool staff = true)
    {
        var client = scenario.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(staff ? "/api/auth/staff/login" : "/api/auth/login", new LoginDto { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }
}

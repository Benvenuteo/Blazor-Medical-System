using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class StaffAccountManagementTests
{
    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("one@example.test", false, HttpStatusCode.Forbidden)]
    [InlineData("doctor@example.test", true, HttpStatusCode.Forbidden)]
    public async Task StaffManagement_RequiresAdministratorForEveryAction(string? email, bool staff, HttpStatusCode expected)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = email is null ? scenario.Factory.CreateClient() : await scenario.SignInAsync(email, staff);
        using var read = await client.GetAsync("/api/staff/accounts");
        using var options = await client.GetAsync("/api/staff/accounts/unassigned-doctors");
        using var get = await client.GetAsync("/api/staff/accounts/1");
        using var create = await client.PostAsJsonAsync("/api/staff/accounts", NewAdmin());
        using var update = await client.PutAsJsonAsync("/api/staff/accounts/1", new UpdateStaffAccountDto());
        using var reset = await client.PostAsJsonAsync("/api/staff/accounts/1/reset-password", new ResetStaffPasswordDto());
        Assert.All(new[] { read, options, get, create, update, reset }, r => Assert.Equal(expected, r.StatusCode));
    }

    [Fact]
    public async Task DirectApplicationCall_AlsoRejectsDoctorRole()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var scope = scenario.Factory.Services.CreateScope();
        var principal = await ScenarioPrincipal.StaffAsync(scope.ServiceProvider, "doctor@example.test");
        var service = ActivatorUtilities.CreateInstance<StaffAccountService>(scope.ServiceProvider, ScenarioPrincipal.Access(scope.ServiceProvider, principal));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.CreateAsync(NewAdmin()));
    }

    [Fact]
    public async Task Create_RestrictsTemporaryCredentials_UntilOwnPasswordChange()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var created = await admin.PostAsJsonAsync("/api/staff/accounts", NewAdmin()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<StaffAccountDto>())!;
        Assert.True(account.MustChangePassword); Assert.NotEqual(Guid.Empty, account.Version);
        AssertNoSecrets(await created.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(created.Headers.Location)).StatusCode);
        using var temporary = await AccountSessionTests.SignInAsync(scenario, account.Email, "TemporaryStaff2026!");
        Assert.Equal(HttpStatusCode.Forbidden, (await temporary.GetAsync("/api/staff/accounts")).StatusCode);
        using var changed = await temporary.PostAsJsonAsync("/api/auth/staff/change-password", new ChangeStaffPasswordDto
        { CurrentPassword = "TemporaryStaff2026!", NewPassword = "PermanentStaff2026!", ConfirmNewPassword = "PermanentStaff2026!" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await temporary.GetAsync("/api/staff/accounts")).StatusCode);
        using var fresh = await AccountSessionTests.SignInAsync(scenario, account.Email, "PermanentStaff2026!");
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/staff/accounts")).StatusCode);
    }

    [Fact]
    public async Task DisableAndReactivate_DoesNotReviveOldTokens()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        var account = await FindAsync(admin, "doctor@example.test");
        using var disable = await admin.PutAsJsonAsync($"/api/staff/accounts/{account.Id}", Update(account, active: false));
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode); account = (await disable.Content.ReadFromJsonAsync<StaffAccountDto>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await doctor.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var anonymous = scenario.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/staff/login", new { email = account.Email, password = SecurityScenario.Password })).StatusCode);
        using var reactivate = await admin.PutAsJsonAsync($"/api/staff/accounts/{account.Id}", Update(account, active: true));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await doctor.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var fresh = await scenario.SignInAsync("doctor@example.test", true);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
    }

    [Fact]
    public async Task Reset_RevokesOldTokens_AndCannotBeRepeatedWithStaleVersion()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        var account = await FindAsync(admin, "doctor@example.test");
        var input = new ResetStaffPasswordDto { NewPassword = "TemporaryStaff2026!", Version = account.Version };
        using var reset = await admin.PostAsJsonAsync($"/api/staff/accounts/{account.Id}/reset-password", input); Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/staff/accounts/{account.Id}/reset-password", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await doctor.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var temporary = await AccountSessionTests.SignInAsync(scenario, account.Email, input.NewPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await temporary.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await temporary.PostAsync("/api/auth/logout-all", null)).StatusCode);
    }

    [Fact]
    public async Task EmailChange_RevokesSessions_AndStaleEditCannotOverwriteIt()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        var account = await FindAsync(admin, "doctor@example.test"); var update = Update(account); update.Email = "NEW-Doctor@example.test";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/staff/accounts/{account.Id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/staff/accounts/{account.Id}", Update(account))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await doctor.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var fresh = await scenario.SignInAsync("new-doctor@example.test", true);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
    }

    [Fact]
    public async Task AdminCannotDisableRenameLoginOrResetOwnAccount()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        var self = await FindAsync(admin, "admin@example.test"); Assert.True(self.IsCurrentUser);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/staff/accounts/{self.Id}", Update(self, active: false))).StatusCode);
        var rename = Update(self); rename.Email = "another@example.test";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/staff/accounts/{self.Id}", rename)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/staff/accounts/{self.Id}/reset-password", new ResetStaffPasswordDto { NewPassword = "TemporaryStaff2026!", Version = self.Version })).StatusCode);
        var display = Update(self); display.DisplayName = "Admin Updated";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/staff/accounts/{self.Id}", display)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/staff/accounts")).StatusCode);
    }

    [Fact]
    public async Task UniqueEmailAndDoctorAssociation_AreEnforced_AndUnassignedOptionsArePaged()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        var duplicate = NewAdmin(); duplicate.Email = "ADMIN@example.test";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/staff/accounts", duplicate)).StatusCode);
        var doctor = NewAdmin(); doctor.Role = "Doctor"; doctor.DoctorId = scenario.DoctorId;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/staff/accounts", doctor)).StatusCode);
        using var scope = scenario.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Doctors.AddRange(Enumerable.Range(1, 3).Select(i => new Doctor { FirstName = "New", LastName = "Doctor " + i, LicenseNumber = "NEW-" + i })); await db.SaveChangesAsync();
        var options = (await admin.GetFromJsonAsync<PagedResult<StaffDoctorOptionDto>>("/api/staff/accounts/unassigned-doctors?page=2&pageSize=2"))!;
        Assert.Equal(3, options.TotalCount); Assert.Single(options.Items);
        doctor.DoctorId = options.Items[0].Id;
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/staff/accounts", doctor)).StatusCode);
        var remaining = (await admin.GetFromJsonAsync<PagedResult<StaffDoctorOptionDto>>("/api/staff/accounts/unassigned-doctors"))!;
        Assert.DoesNotContain(remaining.Items, d => d.Id == doctor.DoctorId);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=51")]
    [InlineData("?page=100001")]
    public async Task AccountSearch_RejectsUnboundedPaging(string query)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/staff/accounts" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/staff/accounts/unassigned-doctors" + query)).StatusCode);
    }

    [Fact]
    public async Task AccountDirectory_UsesStablePages_AndNeverReturnsCredentialFields()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var response = await admin.GetAsync("/api/staff/accounts?page=1&pageSize=2"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = (await response.Content.ReadFromJsonAsync<PagedResult<StaffAccountDto>>())!;
        var second = (await admin.GetFromJsonAsync<PagedResult<StaffAccountDto>>("/api/staff/accounts?page=2&pageSize=2"))!;
        Assert.Equal(4, first.TotalCount); Assert.Equal(2, first.Items.Count); Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(a => a.Id).Intersect(second.Items.Select(a => a.Id)));
        AssertNoSecrets(await response.Content.ReadAsStringAsync());
    }

    internal static CreateStaffAccountDto NewAdmin() => new() { Email = "new-admin@example.test", DisplayName = "New admin", Role = "Admin", InitialPassword = "TemporaryStaff2026!" };
    internal static UpdateStaffAccountDto Update(StaffAccountDto account, bool? active = null) => new() { Email = account.Email, DisplayName = account.DisplayName, IsActive = active ?? account.IsActive, Version = account.Version };
    internal static async Task<StaffAccountDto> FindAsync(HttpClient admin, string email) => Assert.Single((await admin.GetFromJsonAsync<PagedResult<StaffAccountDto>>("/api/staff/accounts?search=" + Uri.EscapeDataString(email)))!.Items, a => a.Email == email);
    private static void AssertNoSecrets(string json)
    { Assert.DoesNotContain("passwordHash", json); Assert.DoesNotContain("securityStamp", json); Assert.DoesNotContain("security_stamp", json); Assert.DoesNotContain("TemporaryStaff2026!", json); }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class StaffSessionTests
{
    [Theory]
    [InlineData("disable")]
    [InlineData("reset")]
    [InlineData("logout-all")]
    [InlineData("password-change")]
    public async Task RevokedCookie_CannotBeReplayedAfterAccountSecurityChange(string change)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var panel = new StaffPanelFactory(scenario.Factory.Connection);
        using var browser = CreateClient(panel); var cookie = await LoginAsync(browser, "doctor@example.test", SecurityScenario.Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/doctor/notes")).StatusCode);
        using var admin = await scenario.SignInAsync("admin@example.test", true);
        var doctor = await StaffAccountManagementTests.FindAsync(admin, "doctor@example.test");
        switch (change)
        {
            case "disable": Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/staff/accounts/{doctor.Id}", StaffAccountManagementTests.Update(doctor, false))).StatusCode); break;
            case "reset": Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/staff/accounts/{doctor.Id}/reset-password", new ResetStaffPasswordDto { NewPassword = "TemporaryStaff2026!", Version = doctor.Version })).StatusCode); break;
            case "logout-all":
                Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Account/LogoutAll", new FormUrlEncodedContent([]))).StatusCode);
                Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Account/LogoutAll", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await CsrfAsync(browser, "/Account/LogoutAll") }))).StatusCode); break;
            case "password-change":
                Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Account/ChangePassword", PasswordForm())).StatusCode);
                Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Account/ChangePassword", PasswordForm(await CsrfAsync(browser, "/Account/ChangePassword")))).StatusCode); break;
        }
        using var replay = panel.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost") });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await replay.GetAsync("/doctor/notes");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        Assert.DoesNotContain("Confidential", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TemporaryStaffCookie_CanOnlyChangePasswordOrLogOut()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var admin = await scenario.SignInAsync("admin@example.test", true);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/staff/accounts", StaffAccountManagementTests.NewAdmin())).StatusCode);
        using var panel = new StaffPanelFactory(scenario.Factory.Connection); using var browser = CreateClient(panel);
        await LoginAsync(browser, "new-admin@example.test", "TemporaryStaff2026!", "/Account/ChangePassword");
        using var restricted = await browser.GetAsync("/admin/accounts");
        Assert.Equal(HttpStatusCode.Redirect, restricted.StatusCode); Assert.Equal("/Account/ChangePassword", restricted.Headers.Location!.ToString());
        var values = new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = "TemporaryStaff2026!", ["Input.NewPassword"] = "PermanentStaff2026!", ["Input.ConfirmNewPassword"] = "PermanentStaff2026!",
            ["__RequestVerificationToken"] = await CsrfAsync(browser, "/Account/ChangePassword")
        };
        using var changed = await browser.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode); Assert.Contains("/Account/Login", changed.Headers.Location!.ToString());
        await LoginAsync(browser, "new-admin@example.test", "PermanentStaff2026!");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/admin/accounts")).StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPassword_DoesNotInvalidateStaffCookie()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var panel = new StaffPanelFactory(scenario.Factory.Connection); using var browser = CreateClient(panel);
        await LoginAsync(browser, "doctor@example.test", SecurityScenario.Password);
        var values = new Dictionary<string, string> { ["Input.CurrentPassword"] = "Incorrect2026!", ["Input.NewPassword"] = "PermanentStaff2026!", ["Input.ConfirmNewPassword"] = "PermanentStaff2026!", ["__RequestVerificationToken"] = await CsrfAsync(browser, "/Account/ChangePassword") };
        using var rejected = await browser.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(values)); Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Aktualne has", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/doctor/notes")).StatusCode);
    }

    private static HttpClient CreateClient(StaffPanelFactory panel) => panel.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
    private static async Task<string> LoginAsync(HttpClient client, string email, string password, string destination = "/")
    {
        var csrf = await CsrfAsync(client, "/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = csrf }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal(destination, response.Headers.Location!.ToString());
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("MedicalStaff=")).Split(';')[0];
    }
    private static async Task<string> CsrfAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path); var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent PasswordForm(string? csrf = null)
    {
        var values = new Dictionary<string, string> { ["Input.CurrentPassword"] = SecurityScenario.Password, ["Input.NewPassword"] = "PermanentStaff2026!", ["Input.ConfirmNewPassword"] = "PermanentStaff2026!" };
        if (csrf is not null) values["__RequestVerificationToken"] = csrf; return new(values);
    }
}

using System.Net;
using System.Text.RegularExpressions;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class StaffPanelAuthenticationTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/admin/doctors")]
    [InlineData("/doctor/notes")]
    [InlineData("/doctor/prescriptions")]
    public async Task StaffPage_WithoutSession_RedirectsToLogin(string path)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var panel = new StaffPanelFactory(scenario.Factory.Connection);
        using var client = CreateClient(panel);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LoginAndLogout_RequireAntiforgeryToken_AndManageSessionCookie()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var panel = new StaffPanelFactory(scenario.Factory.Connection);
        using var client = CreateClient(panel);
        using var loginWithoutCsrf = await client.PostAsync("/Account/Login", Form("admin@example.test"));
        Assert.Equal(HttpStatusCode.BadRequest, loginWithoutCsrf.StatusCode);
        var token = await ReadAntiforgeryToken(client, "/Account/Login");
        using var login = await client.PostAsync("/Account/Login?ReturnUrl=https://example.com", Form("admin@example.test", token));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location!.ToString());
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("MedicalStaff=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        using var authorized = await client.GetAsync("/admin/doctors");
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Contains("Lista lekarzy", await authorized.Content.ReadAsStringAsync());
        using var logoutWithoutCsrf = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, logoutWithoutCsrf.StatusCode);
        // GET renders a confirmation; it cannot log a user out via a third-party image/link.
        token = await ReadAntiforgeryToken(client, "/Account/Logout");
        using var stillAuthorized = await client.GetAsync("/admin/doctors");
        Assert.Equal(HttpStatusCode.OK, stillAuthorized.StatusCode);
        using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/admin/doctors");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
    }

    [Fact]
    public async Task DoctorSession_ShowsOwnClinicalPages_AndHidesAdminPanel()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var panel = new StaffPanelFactory(scenario.Factory.Connection);
        using var client = CreateClient(panel);
        var token = await ReadAntiforgeryToken(client, "/Account/Login");
        using var login = await client.PostAsync("/Account/Login", Form("doctor@example.test", token));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var clinical = await client.GetAsync("/doctor/notes");
        Assert.Equal(HttpStatusCode.OK, clinical.StatusCode);
        Assert.Contains($"Pacjent ID:</b> {scenario.PatientId}", await clinical.Content.ReadAsStringAsync());
        using var admin = await client.GetAsync("/admin/doctors");
        var adminBody = await admin.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Lista lekarzy", adminBody);
        Assert.Contains("Brak dost", adminBody);
    }

    [Theory]
    [InlineData("inactive@example.test")]
    [InlineData("one@example.test")]
    public async Task InactiveStaffOrPatient_CannotSignIntoStaffPanel(string email)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var panel = new StaffPanelFactory(scenario.Factory.Connection);
        using var client = CreateClient(panel);
        var token = await ReadAntiforgeryToken(client, "/Account/Login");
        using var login = await client.PostAsync("/Account/Login", Form(email, token));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain(login.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], c => c.StartsWith("MedicalStaff="));
        using var protectedPage = await client.GetAsync("/admin/doctors");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    private static HttpClient CreateClient(StaffPanelFactory panel) => panel.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost")
    });

    private static FormUrlEncodedContent Form(string email, string? csrf = null)
    {
        var values = new Dictionary<string, string> { ["Email"] = email, ["Password"] = SecurityScenario.Password };
        if (csrf is not null) values["__RequestVerificationToken"] = csrf;
        return new(values);
    }

    private static async Task<string> ReadAntiforgeryToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "The Razor form must render an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}

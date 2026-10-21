using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Services;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class PatientAuthenticationTests : BunitContext
{
    private readonly Dictionary<string, string> _items = new();
    private readonly Mock<ILocalStorageService> _storage = new();

    public PatientAuthenticationTests()
    {
        _storage.Setup(s => s.GetItemAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => ValueTask.FromResult<string?>(_items.GetValueOrDefault(key)));
        _storage.Setup(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, string value, CancellationToken _) => _items[key] = value).Returns(ValueTask.CompletedTask);
        _storage.Setup(s => s.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => _items.Remove(key)).Returns(ValueTask.CompletedTask);
    }

    [Fact]
    public async Task SignInAndLogout_PersistToken_AndRemoveLegacyPatientId()
    {
        _items["patientId"] = "999";
        using var authentication = new PatientAuthenticationStateProvider(_storage.Object);
        await authentication.SignInAsync(Token());
        Assert.True((await authentication.GetAuthenticationStateAsync()).User.IsInRole(Roles.Patient));
        Assert.False(_items.ContainsKey("patientId"));
        await authentication.LogoutAsync();
        Assert.False((await authentication.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Empty(_items);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("staff")]
    public async Task InvalidOrExpiredOrStaffToken_ClearsSession(string kind)
    {
        _items[PatientAuthenticationStateProvider.TokenKey] = kind == "malformed" ? "broken.token" : Token(kind == "staff" ? Roles.Doctor : Roles.Patient, kind == "expired" ? -60 : 300);
        _items["patientId"] = "999";
        using var authentication = new PatientAuthenticationStateProvider(_storage.Object);
        Assert.False((await authentication.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Empty(_items);
    }

    [Fact]
    public async Task Handler_AttachesTokenToApiOnly_And401ClearsSession()
    {
        using var authentication = new PatientAuthenticationStateProvider(_storage.Object);
        var token = Token();
        await authentication.SignInAsync(token);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var recorder = new RecordingHandler();
        using var client = new HttpClient(new ApiAuthenticationHandler(authentication, navigation, new Uri("https://api.test/")) { InnerHandler = recorder });
        using var success = await client.GetAsync("https://api.test/api/appointments/upcoming");
        Assert.Equal("Bearer " + token, recorder.Authorization);
        using var external = await client.GetAsync("https://external.test/");
        Assert.Null(recorder.Authorization);
        recorder.Status = HttpStatusCode.Unauthorized;
        using var rejected = await client.GetAsync("https://api.test/api/patients/profile");
        Assert.False((await authentication.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.EndsWith("/", navigation.Uri);
    }

    private static string Token(string role = Roles.Patient, int seconds = 300)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["exp"] = expires, [ClaimTypes.Role] = role, [AccountClaims.PatientId] = "7", [AccountClaims.ExpiresAt] = expires.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "header." + payload + ".signature";
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }
}

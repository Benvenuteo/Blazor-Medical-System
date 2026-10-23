using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace MedicalBookingSystem.BlazorWasm.Services;

/// <summary>Decodes claims only for the UI; the API validates the signature and resource ownership.</summary>
public sealed class PatientAuthenticationStateProvider(ILocalStorageService storage) : AuthenticationStateProvider, IDisposable
{
    public const string TokenKey = "medical_access_token";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private Timer? _expiryTimer;
    private bool _wasAuthenticated;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetTokenAsync();
        return new AuthenticationState(token is null ? Anonymous : ReadPrincipal(token));
    }

    public async Task<string?> GetTokenAsync()
    {
        var token = await storage.GetItemAsync<string>(TokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            if (_wasAuthenticated) await LogoutAsync();
            return null;
        }
        var user = ReadPrincipal(token);
        if (user.Identity?.IsAuthenticated != true)
        {
            await LogoutAsync();
            return null;
        }
        _wasAuthenticated = true;
        ScheduleExpiry(user);
        return token;
    }

    public async Task SignInAsync(string token)
    {
        var user = ReadPrincipal(token);
        if (user.Identity?.IsAuthenticated != true) throw new InvalidOperationException("Nieprawidłowy token logowania.");
        await storage.SetItemAsync(TokenKey, token);
        await storage.RemoveItemAsync("patientId");
        _wasAuthenticated = true;
        ScheduleExpiry(user);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task LogoutAsync()
    {
        _wasAuthenticated = false;
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        await storage.RemoveItemAsync(TokenKey);
        await storage.RemoveItemAsync("patientId");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private void ScheduleExpiry(ClaimsPrincipal user)
    {
        var expires = long.Parse(user.FindFirst(AccountClaims.ExpiresAt)?.Value!, System.Globalization.CultureInfo.InvariantCulture);
        var remaining = DateTimeOffset.FromUnixTimeSeconds(expires) - DateTimeOffset.UtcNow;
        _expiryTimer?.Dispose();
        _expiryTimer = new Timer(state => { _ = ExpireAsync(); }, null,
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private async Task ExpireAsync()
    {
        try { await LogoutAsync(); }
        catch (Microsoft.JSInterop.JSDisconnectedException) { /* The browser has already closed. */ }
    }

    private static ClaimsPrincipal ReadPrincipal(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return Anonymous;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = json.RootElement;
            if (!root.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var expiration) ||
                (expiration <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || expiration > DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds())) return Anonymous;
            var claims = root.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .Select(p => new Claim(p.Name, p.Value.GetString()!)).ToList();
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "BrowserToken"));
            if (!user.IsInRole(Roles.Patient) || AccountClaims.ReadId(user, AccountClaims.PatientId) is null ||
                !long.TryParse(user.FindFirst(AccountClaims.ExpiresAt)?.Value, out var sessionExpiry) ||
                sessionExpiry != expiration) return Anonymous;
            return user;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return Anonymous;
        }
    }

    public void Dispose() => _expiryTimer?.Dispose();
}

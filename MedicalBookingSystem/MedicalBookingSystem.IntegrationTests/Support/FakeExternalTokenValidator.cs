using System.Collections.Concurrent;
using MedicalBookingSystem.Application.Security;

namespace MedicalBookingSystem.IntegrationTests.Support;

/// <summary>
/// Stands in for Google in tests: a token is valid only when it was issued here and is presented with its nonce.
/// Signature, issuer and audience checks of the real validator are unit-free IdentityModel code.
/// </summary>
internal sealed class FakeExternalTokenValidator : IExternalTokenValidator
{
    private readonly ConcurrentDictionary<string, (string Nonce, ExternalIdentity Identity)> tokens = new();
    public bool GoogleEnabled { get; set; } = true;

    public (string IdToken, string Nonce) Issue(string subject, string email, bool verified = true, string? given = "Gosia", string? family = "Google")
    {
        var token = "google-" + Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        tokens[token] = (nonce, new ExternalIdentity(ExternalAuthenticationService.Google, subject, email, verified, given, family, null));
        return (token, nonce);
    }

    public Task<ExternalIdentity?> ValidateGoogleAsync(string idToken, string nonce, CancellationToken cancellationToken = default) =>
        Task.FromResult(tokens.TryGetValue(idToken, out var entry) && entry.Nonce == nonce ? entry.Identity : null);
}

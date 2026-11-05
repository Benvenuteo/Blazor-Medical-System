using System.IdentityModel.Tokens.Jwt;
using MedicalBookingSystem.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MedicalBookingSystem.WebAPI.Security;

public sealed class GoogleAuthenticationOptions
{
    /// <summary>OAuth client ID of type "Web application". Empty disables Google sign-in.</summary>
    public string ClientId { get; set; } = string.Empty;
    public string MetadataAddress { get; set; } = "https://accounts.google.com/.well-known/openid-configuration";
}

/// <summary>
/// Validates Google ID tokens with the same IdentityModel stack as the API's own JWTs: Google's published signing
/// keys (cached and refreshed by the configuration manager), issuer, audience = our client ID, lifetime and nonce.
/// </summary>
public sealed class GoogleIdTokenValidator : IExternalTokenValidator
{
    private static readonly string[] Issuers = ["https://accounts.google.com", "accounts.google.com"];
    private readonly GoogleAuthenticationOptions options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? metadata;
    private readonly ILogger<GoogleIdTokenValidator> logger;

    public GoogleIdTokenValidator(IOptions<GoogleAuthenticationOptions> options, ILogger<GoogleIdTokenValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        if (GoogleEnabled)
            metadata = new ConfigurationManager<OpenIdConnectConfiguration>(this.options.MetadataAddress,
                new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true });
    }

    public bool GoogleEnabled => !string.IsNullOrWhiteSpace(options.ClientId);

    public async Task<ExternalIdentity?> ValidateGoogleAsync(string idToken, string nonce, CancellationToken cancellationToken = default)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192 || string.IsNullOrWhiteSpace(nonce)) return null;
        try
        {
            var configuration = await metadata.GetConfigurationAsync(cancellationToken);
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidIssuers = Issuers, ValidAudience = options.ClientId, IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(2), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            }, out _);
            string? Claim(string type) => principal.FindFirst(type)?.Value;
            if (!string.Equals(Claim("nonce"), nonce, StringComparison.Ordinal) || Claim("sub") is not { Length: > 0 } subject) return null;
            return new ExternalIdentity(ExternalAuthenticationService.Google, subject, Claim("email") ?? string.Empty,
                string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
                Claim("given_name"), Claim("family_name"), Claim("name"));
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException or InvalidOperationException)
        {
            logger.LogInformation("Rejected Google ID token: {Reason}", exception.GetType().Name);
            return null;
        }
    }
}

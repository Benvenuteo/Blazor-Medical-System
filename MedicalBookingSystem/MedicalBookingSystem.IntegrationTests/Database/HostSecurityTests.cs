using Microsoft.AspNetCore.Builder;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MedicalBookingSystem.Infrastructure.Hosting;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class HostSecurityTests
{
    [Fact]
    public void MissingProxyTrust_DisablesForwardedHeaders_AndExplicitTrustDoesNotAllowArbitraryHosts()
    {
        var services = new ServiceCollection(); services.AddHostSecurity(Configuration([]), new HostEnvironment(), false);
        using var provider = services.BuildServiceProvider(); Assert.Equal(ForwardedHeaders.None, provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value.ForwardedHeaders);
        var trusted = new ServiceCollection(); trusted.AddHostSecurity(Configuration(new() { ["Proxy:KnownProxies:0"] = "192.0.2.5" }), new HostEnvironment(), false);
        using var configured = trusted.BuildServiceProvider(); var options = configured.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal("192.0.2.5", Assert.Single(options.KnownProxies).ToString()); Assert.Empty(options.KnownIPNetworks); Assert.Equal(1, options.ForwardLimit);
    }

    [Theory]
    [InlineData("AllowedHosts")]
    [InlineData("DataProtection:KeysPath")]
    public void ProductionHost_RejectsMissingSecurityConfiguration(string missing)
    {
        var values = new Dictionary<string, string?> { ["AllowedHosts"] = "staff.example.test" };
        values.Remove(missing);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddHostSecurity(Configuration(values), new HostEnvironment { EnvironmentName = "Production" }, true));
    }

    [Fact]
    public void EncryptedPersistentKeys_CanUnprotectAfterHostRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "medical-key-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=Medical local key test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var pfx = Path.Combine(root, "keys.pfx"); File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pfx, "test-password"));
            var configuration = Configuration(new() { ["AllowedHosts"] = "staff.example.test", ["DataProtection:KeysPath"] = Path.Combine(root, "ring"), ["DataProtection:CertificatePath"] = pfx, ["DataProtection:CertificatePassword"] = "test-password" });
            string protectedValue;
            using (var first = Provider(configuration)) protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Protect("session-payload");
            using (var second = Provider(configuration)) Assert.Equal("session-payload", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Unprotect(protectedValue));
            Assert.All(Directory.GetFiles(Path.Combine(root, "ring")), path => Assert.Contains("encryptedSecret", File.ReadAllText(path)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StaffResponses_ProtectFramingContentTypeAndCache()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var staff = new StaffPanelFactory(scenario.Factory.Connection); using var client = staff.CreateClient();
        using var response = await client.GetAsync("/Account/Login");
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("script-src 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy"))); Assert.True(response.Headers.CacheControl!.NoStore);
    }
    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static ServiceProvider Provider(IConfiguration configuration)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddHostSecurity(configuration, new HostEnvironment { EnvironmentName = "Production" }, true); return services.BuildServiceProvider();
    }
    private sealed class HostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "HostSecurityTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

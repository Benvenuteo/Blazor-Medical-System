using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MedicalBookingSystem.Infrastructure.Hosting;

public static class HostSecurity
{
    public static IServiceCollection AddHostSecurity(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment, bool staff)
    {
        if (configuration.GetValue<bool>("ForwardedHeaders_Enabled") ||
            Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED") == "true" ||
            Environment.GetEnvironmentVariable("DOTNET_FORWARDEDHEADERS_ENABLED") == "true")
            throw new InvalidOperationException("Use Proxy:KnownProxies instead of globally enabling forwarded headers.");
        var proxies = configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // An empty trust list disables processing rather than accepting all proxies.
            options.ForwardedHeaders = proxies.Length == 0 ? ForwardedHeaders.None : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1; options.RequireHeaderSymmetry = true;
            if (proxies.Length > 0)
            {
                options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
                foreach (var proxy in proxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        if (environment.IsProduction())
        {
            var hosts = configuration["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(hosts) || hosts.Contains('*'))
                throw new InvalidOperationException("Configure explicit AllowedHosts for Production.");
            if (staff)
            {
                var keyPath = configuration["DataProtection:KeysPath"];
                var certificatePath = configuration["DataProtection:CertificatePath"];
                if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(certificatePath))
                    throw new InvalidOperationException("Configure persistent DataProtection:KeysPath and CertificatePath for the staff host.");
                var full = Path.GetFullPath(keyPath, environment.ContentRootPath);
                var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
                if (full == webRoot || full.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Data Protection keys must be outside the web root.");
                Directory.CreateDirectory(full);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(full, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.GetFullPath(certificatePath, environment.ContentRootPath), configuration["DataProtection:CertificatePassword"]);
                if (!certificate.HasPrivateKey) throw new InvalidOperationException("Data Protection certificate needs its private key.");
                services.AddDataProtection().SetApplicationName("MedicalBookingSystem.Staff")
                    .PersistKeysToFileSystem(new DirectoryInfo(full)).ProtectKeysWithCertificate(certificate);
            }
        }
        return services;
    }

    public static IApplicationBuilder UseHostSecurity(this IApplicationBuilder app, bool staff)
    {
        app.UseForwardedHeaders();
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (staff)
            {
                var websocket = $"{(context.Request.IsHttps ? "wss" : "ws")}://{context.Request.Host}";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                    $"connect-src 'self' {websocket}; img-src 'self' data: https:; font-src 'self'; " +
                    "object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            }
            if (context.Request.Path.StartsWithSegments("/api") || staff && !context.Request.Path.StartsWithSegments("/media"))
                context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
    }
}

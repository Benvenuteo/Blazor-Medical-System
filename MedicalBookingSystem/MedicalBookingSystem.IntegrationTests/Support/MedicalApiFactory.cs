using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MedicalBookingSystem.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedicalBookingSystem.IntegrationTests.Support;

/// <summary>Runs the real HTTP pipeline against an isolated relational database for each test.</summary>
internal sealed class MedicalApiFactory : WebApplicationFactory<MedicalBookingSystem.WebAPI.ApiEntryPoint>
{
    private readonly SqliteConnection? _connection;
    private readonly string? _connectionString;
    private readonly TimeProvider? _testClock;
    private readonly IInterceptor? _interceptor;

    public MedicalApiFactory(string? connectionString = null, TimeProvider? testClock = null, IInterceptor? interceptor = null)
    {
        _connectionString = connectionString;
        _testClock = testClock;
        _interceptor = interceptor;
        if (connectionString is null) { _connection = new("Data Source=:memory:"); _connection.Open(); }
    }
    internal SqliteConnection Connection => _connection ?? throw new InvalidOperationException("This factory uses independent file connections.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "integration-tests-only-signing-key-at-least-thirty-two-bytes",
            ["Seed:Enabled"] = "false",
            ["Database:InitializeOnStartup"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (_connectionString is null) options.UseSqlite(_connection!);
                else options.UseSqlite(_connectionString);
                if (_interceptor is not null) options.AddInterceptors(_interceptor);
            });
            if (_testClock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(_testClock);
                // Only time is controlled: signatures, algorithm, issuer, audience and roles still use the real handler.
                services.PostConfigure<JwtBearerOptions>("Bearer", options => options.TokenValidationParameters.LifetimeValidator =
                    (notBefore, expires, _, parameters) =>
                    {
                        var now = _testClock.GetUtcNow().UtcDateTime;
                        return expires.HasValue && (!notBefore.HasValue || notBefore <= now + parameters.ClockSkew) &&
                            expires > now - parameters.ClockSkew && (!notBefore.HasValue || notBefore < expires);
                    });
            }
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection?.Dispose();
    }
}

using Microsoft.AspNetCore.DataProtection;
using MedicalBookingSystem.BlazorServer;
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

internal sealed class StaffPanelFactory(SqliteConnection connection) : WebApplicationFactory<StaffEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Media:PublicBaseUrl"] = "http://localhost",
            ["Media:StoragePath"] = Path.Combine(Path.GetTempPath(), "medical-staff-test-media")
        }));
        builder.ConfigureServices(services =>
        {
            // Keep test cookies independent of the developer profile and other test hosts.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection), ServiceLifetime.Scoped);
        });
    }
}

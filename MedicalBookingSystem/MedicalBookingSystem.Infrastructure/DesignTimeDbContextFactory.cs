using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedicalBookingSystem.Infrastructure;

/// <summary>Lets migration tooling inspect the schema without starting either web host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MEDICAL_MIGRATION_CONNECTION")
            ?? "Data Source=data/medical.db";
        connectionString = DependencyInjection.ResolveConnectionString(connectionString, Directory.GetCurrentDirectory());
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString).Options);
    }
}

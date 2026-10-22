using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Infrastructure.Queries;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Resolves relative database files against the solution directory or the published application.</summary>
    public static string ResolveConnectionString(string connectionString, string contentRootPath)
    {
        var connection = new SqliteConnectionStringBuilder(connectionString);
        if (connection.DataSource == ":memory:" || connection.Mode == SqliteOpenMode.Memory)
            return connection.ToString();

        DirectoryInfo? directory = new(contentRootPath);
        var basePath = contentRootPath;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MedicalBookingSystem.sln")))
            {
                basePath = directory.FullName;
                break;
            }
            var nestedSolution = Path.Combine(directory.FullName, "MedicalBookingSystem");
            if (File.Exists(Path.Combine(nestedSolution, "MedicalBookingSystem.sln")))
            {
                basePath = nestedSolution;
                break;
            }
            directory = directory.Parent;
        }
        connection.DataSource = Path.GetFullPath(connection.DataSource, basePath);
        Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
        return connection.ToString();
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connectionString), ServiceLifetime.Scoped);
        services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped<IDoctorDirectoryQueries, DoctorDirectoryQueries>();
        services.AddScoped<IStaffAccountQueries, StaffAccountQueries>();
        services.AddScoped<DataSeeder>();
        return services;
    }
}

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
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IDoctorScheduleRepository, DoctorScheduleRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ISpecializationRepository, SpecializationRepository>();
        services.AddScoped<DataSeeder>();
        return services;
    }
}

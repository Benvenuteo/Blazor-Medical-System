using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class SqliteDatabaseTests
{
    [Fact]
    public async Task Migrations_CreateSchemaAndAreRepeatable()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await database.Patients.CountAsync());
    }

    [Fact]
    public async Task PatientEmail_UniqueConstraint_IsCaseInsensitive()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.Patients.Add(PatientWithEmail("patient@example.test"));
        await database.SaveChangesAsync();

        database.Patients.Add(PatientWithEmail("PATIENT@EXAMPLE.TEST"));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task Appointment_WithNonexistentDoctorAndPatient_IsRejectedByForeignKeys()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.Appointments.Add(new Appointment { DoctorId = 404, PatientId = 404, Date = new DateTime(2026, 11, 1) });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task DoctorDetails_ProjectsRatingsAndUpcomingSchedulesWithoutTrackingGraph()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doctor = new Doctor { FirstName = "Anna", LastName = "Testowa", LicenseNumber = "TEST-001" };
        doctor.Reviews.Add(new Review { Doctor = doctor, Patient = PatientWithEmail("patient@example.test"), Rating = 5 });
        doctor.Schedules.Add(new DoctorSchedule
        {
            Doctor = doctor, StartTime = new DateTime(2026, 11, 1, 9, 0, 0), EndTime = new DateTime(2026, 11, 1, 12, 0, 0)
        });
        database.Doctors.Add(doctor);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var queries = scope.ServiceProvider.GetRequiredService<MedicalBookingSystem.Application.Queries.IDoctorDirectoryQueries>();
        var loaded = await queries.GetDetailsAsync(doctor.Id, new DateTime(2026, 10, 1), new DateTime(2026, 12, 1));
        Assert.NotNull(loaded);
        Assert.Single(loaded.Schedules);
        Assert.Equal(1, loaded.ReviewCount);
        Assert.Equal(5, loaded.AverageRating);
        Assert.Empty(database.ChangeTracker.Entries());
    }

    private static Patient PatientWithEmail(string email) => new()
    {
        Email = email, FirstName = "Jan", LastName = "Testowy", PasswordHash = "not-used-by-database-test",
        DateOfBirth = new DateTime(1990, 1, 1)
    };
}

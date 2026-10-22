using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class AccountIntegrityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_RejectsRemovingLastActiveAdministrator(bool delete)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.StaffAccounts.SingleAsync(a => a.Email == "admin@example.test");
        if (delete) db.StaffAccounts.Remove(admin); else admin.IsActive = false;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("last_active_administrator", error.InnerException!.Message);
        db.ChangeTracker.Clear(); Assert.Equal(1, await db.StaffAccounts.CountAsync(a => a.Role == StaffRole.Admin && a.IsActive));
    }

    [Fact]
    public async Task CompetingAdministratorDeactivations_LeaveOneActiveAccount_AndMapConflict()
    {
        using var file = new TemporarySqliteDatabase(); using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(file.ConnectionString));
        using (var seed = scenario.Factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StaffAccounts.Add(new StaffAccount { Email = "second-admin@example.test", DisplayName = "Second admin", Role = StaffRole.Admin, PasswordHash = "test-only" }); await db.SaveChangesAsync();
        }
        using var firstScope = scenario.Factory.Services.CreateScope(); using var secondScope = scenario.Factory.Services.CreateScope();
        using var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create(); using var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
        var accounts = await firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StaffAccounts.AsNoTracking().Where(a => a.Role == StaffRole.Admin && a.IsActive).OrderBy(a => a.Id).ToListAsync();
        (await first.StaffAccounts.GetByIdAsync(accounts[0].Id))!.IsActive = false;
        (await second.StaffAccounts.GetByIdAsync(accounts[1].Id))!.IsActive = false;
        static async Task<PersistenceConflictKind?> SaveAsync(IUnitOfWork unit)
        { try { await unit.SaveChangesAsync(); return null; } catch (PersistenceConflictException e) { return e.Kind; } }
        var results = await Task.WhenAll(Task.Run(() => SaveAsync(first)), Task.Run(() => SaveAsync(second)));
        Assert.Single(results, r => r is null); Assert.Single(results, r => r == PersistenceConflictKind.LastAdministrator);
        using var check = scenario.Factory.Services.CreateScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().StaffAccounts.CountAsync(a => a.Role == StaffRole.Admin && a.IsActive));
    }

    [Fact]
    public async Task AccountMigration_BackfillsCanonicalDistinctStamps_AndPreservesCredentialsAndBookingTriggers()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261021222721_AppointmentIntegrity");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Patients (Email, FirstName, LastName, PasswordHash, DateOfBirth, PhoneNumber)
            VALUES ('legacy@example.test', 'Legacy', 'Patient', 'preserved-hash', '1990-01-01', '');
            INSERT INTO StaffAccounts (Email, DisplayName, PasswordHash, Role, IsActive, DoctorId)
            VALUES ('legacy-admin@example.test', 'Legacy admin', 'preserved-staff-hash', 'Admin', 1, NULL);
            """);
        await db.Database.MigrateAsync();
        var patient = await db.Patients.SingleAsync(); var staff = await db.StaffAccounts.SingleAsync();
        Assert.Equal("preserved-hash", patient.PasswordHash); Assert.Equal("preserved-staff-hash", staff.PasswordHash);
        Assert.Equal(4, new[] { patient.SecurityStamp, patient.Version, staff.SecurityStamp, staff.Version }.Distinct().Count());
        Assert.All(new[] { patient.SecurityStamp, patient.Version, staff.SecurityStamp, staff.Version }, value => Assert.NotEqual(Guid.Empty, value));
        patient.FirstName = "Updated"; staff.DisplayName = "Updated"; await db.SaveChangesAsync();
        // Saving migrated rows proves GUID text also matches optimistic concurrency predicates.
        Assert.Equal(4L, await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'trigger' AND (name LIKE 'Appointment_NoOverlap_%' OR name LIKE 'Schedule_NoOverlap_%')").SingleAsync());
    }
}

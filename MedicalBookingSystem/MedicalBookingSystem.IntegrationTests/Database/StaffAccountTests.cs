using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class StaffAccountTests
{
    [Theory]
    [InlineData(StaffRole.Doctor)]
    [InlineData(StaffRole.Admin)]
    public async Task Database_RejectsInvalidRoleDoctorAssociation(StaffRole role)
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doctor = new Doctor { LicenseNumber = "TEST-1", FirstName = "Test", LastName = "Doctor" };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();
        db.StaffAccounts.Add(new StaffAccount { Email = "staff@example.test", DisplayName = "Staff", Role = role, DoctorId = role == StaffRole.Admin ? doctor.Id : null, PasswordHash = "test-only" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DemoSeed_CreatesHashedRoleAccounts_AndRepeatingDoesNotResetCredentials()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<StaffAccount>>();
        var seed = new DataSeeder(db, services.GetRequiredService<ClinicClock>(), services.GetRequiredService<IPasswordHasher<Patient>>(), hasher,
            Options.Create(new DemoDataOptions { Enabled = true, PatientPassword = "DemoPatient2026!", StaffPassword = "DemoStaff2026!" }));
        await seed.SeedAsync();
        var doctor = await db.StaffAccounts.SingleAsync(a => a.Role == StaffRole.Doctor);
        var admin = await db.StaffAccounts.SingleAsync(a => a.Role == StaffRole.Admin);
        Assert.NotNull(doctor.DoctorId);
        Assert.Null(admin.DoctorId);
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(doctor, doctor.PasswordHash, "DemoStaff2026!"));
        var changedHash = hasher.HashPassword(doctor, "Changed2026!");
        doctor.PasswordHash = changedHash;
        await db.SaveChangesAsync();
        await seed.SeedAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.StaffAccounts.CountAsync());
        Assert.Equal(changedHash, (await db.StaffAccounts.SingleAsync(a => a.Role == StaffRole.Doctor)).PasswordHash);
    }
}

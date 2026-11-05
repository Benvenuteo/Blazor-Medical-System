using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class DemoAccountTests
{
    [Fact]
    public async Task DemoPatient_CannotChangeProfilePasswordOrSessions_ButKeepsUsingTheApp()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await MarkDemoAsync(scenario, patientEmail: "one@example.test");
        using var client = await scenario.SignInAsync();

        using var password = await client.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto
            { CurrentPassword = SecurityScenario.Password, NewPassword = "Changed2026!", ConfirmNewPassword = "Changed2026!" });
        using var name = await client.PutAsJsonAsync("/api/patients/profile", new UpdatePatientDto { FirstName = "Changed" });
        using var logoutAll = await client.PostAsync("/api/auth/logout-all", null);
        using var profile = await client.GetAsync("/api/patients/profile");
        using var history = await client.GetAsync("/api/appointments/history");

        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
        Assert.Contains("demonstracyjne", await password.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, name.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, logoutAll.StatusCode);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("First", (await profile.Content.ReadFromJsonAsync<PatientDto>())!.FirstName);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var again = await scenario.SignInAsync();
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/patients/profile")).StatusCode);
    }

    [Fact]
    public async Task DemoStaffAccount_CannotBeEditedResetOrChangeItsOwnPassword()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await MarkDemoAsync(scenario, staffEmail: "doctor@example.test");
        var (id, version) = await StaffAsync(scenario, "doctor@example.test");
        using var admin = await scenario.SignInAsync("admin@example.test", staff: true);
        using var doctor = await scenario.SignInAsync("doctor@example.test", staff: true);

        using var edit = await admin.PutAsJsonAsync($"/api/staff/accounts/{id}", new UpdateStaffAccountDto
            { Email = "doctor@example.test", DisplayName = "Renamed", IsActive = false, Version = version });
        using var reset = await admin.PostAsJsonAsync($"/api/staff/accounts/{id}/reset-password", new ResetStaffPasswordDto
            { NewPassword = "TemporaryReset2026!", Version = version });
        using var own = await doctor.PostAsJsonAsync("/api/auth/staff/change-password", new ChangeStaffPasswordDto
            { CurrentPassword = SecurityScenario.Password, NewPassword = "ChangedStaff2026!", ConfirmNewPassword = "ChangedStaff2026!" });
        var listed = await admin.GetFromJsonAsync<MedicalBookingSystem.SharedKernel.PagedResult<StaffAccountDto>>("/api/staff/accounts?search=doctor@example.test");

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.True(listed!.Items.Single(a => a.Id == id).IsDemo);
        Assert.Equal(HttpStatusCode.OK, (await doctor.GetAsync("/api/doctor/appointments")).StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StaffAccounts.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.True(account.IsActive);
        Assert.Equal("First Doctor", account.DisplayName);
    }

    [Fact]
    public async Task OtherAccounts_RemainManageableNextToDemoAccounts()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await MarkDemoAsync(scenario, staffEmail: "doctor@example.test");
        var (id, version) = await StaffAsync(scenario, "other-doctor@example.test");
        using var admin = await scenario.SignInAsync("admin@example.test", staff: true);

        using var reset = await admin.PostAsJsonAsync($"/api/staff/accounts/{id}/reset-password", new ResetStaffPasswordDto
            { NewPassword = "TemporaryReset2026!", Version = version });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task Seeder_MarksDemoAccounts_AndTopsUpWorkingDaysWithoutDuplicates()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var clock = services.GetRequiredService<ClinicClock>();
        var seeder = new DataSeeder(db, clock, services.GetRequiredService<IPasswordHasher<Patient>>(),
            services.GetRequiredService<IPasswordHasher<StaffAccount>>(),
            Options.Create(new DemoDataOptions { Enabled = true, PatientPassword = "DemoPatient2026!", StaffPassword = "DemoStaff2026!" }));

        await seeder.SeedAsync();
        var addedAgain = await seeder.TopUpScheduleAsync();

        Assert.True(await db.Patients.AnyAsync(p => p.Email == DemoAccounts.PatientEmail && p.IsDemo));
        Assert.Equal(2, await db.StaffAccounts.CountAsync(a => a.IsDemo));
        Assert.Equal(0, addedAgain);
        var horizon = clock.Today.AddDays(DataSeeder.ScheduleHorizonDays);
        var days = (await db.DoctorSchedules.Select(s => s.StartTime).ToListAsync()).Select(d => d.Date).ToList();
        Assert.Equal(days.Count, days.Distinct().Count());
        for (var day = clock.Today.AddDays(1); day <= horizon; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                Assert.Contains(day, days);
    }

    [Fact]
    public async Task Seeder_ProtectsDemoAccountsCreatedBeforeTheFlagExisted()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Doctors.Add(new Doctor { FirstName = "Demo", LastName = "Doctor", LicenseNumber = DataSeeder.DemoDoctorLicense });
        await db.SaveChangesAsync();
        var seeder = new DataSeeder(db, services.GetRequiredService<ClinicClock>(), services.GetRequiredService<IPasswordHasher<Patient>>(),
            services.GetRequiredService<IPasswordHasher<StaffAccount>>(),
            Options.Create(new DemoDataOptions { Enabled = true, PatientPassword = "x", StaffPassword = "y" }));

        await seeder.SeedAsync();

        // The scenario's doctor@ and admin@ accounts use the public demo addresses; ordinary accounts stay manageable.
        var demo = await db.StaffAccounts.AsNoTracking().Where(a => a.IsDemo).Select(a => a.Email).OrderBy(e => e).ToListAsync();
        Assert.Equal(["admin@example.test", "doctor@example.test"], demo);
        Assert.False(await db.Patients.AnyAsync(p => p.IsDemo));
    }

    private static async Task MarkDemoAsync(SecurityScenario scenario, string? patientEmail = null, string? staffEmail = null)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (patientEmail is not null) await db.Patients.Where(p => p.Email == patientEmail).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDemo, true));
        if (staffEmail is not null) await db.StaffAccounts.Where(a => a.Email == staffEmail).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDemo, true));
    }

    private static async Task<(int Id, Guid Version)> StaffAsync(SecurityScenario scenario, string email)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StaffAccounts.AsNoTracking().SingleAsync(a => a.Email == email);
        return (account.Id, account.Version);
    }
}

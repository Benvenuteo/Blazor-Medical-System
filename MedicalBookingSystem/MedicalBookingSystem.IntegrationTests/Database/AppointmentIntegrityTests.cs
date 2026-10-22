using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class AppointmentIntegrityTests
{
    [Fact]
    public async Task StaleWriter_CannotOverwriteCommittedCancellation_AndTrackerIsCleared()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var firstScope = scenario.Factory.Services.CreateAsyncScope();
        await using var secondScope = scenario.Factory.Services.CreateAsyncScope();
        using var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
        using var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
        var a = (await first.Appointments.GetByIdAsync(scenario.AppointmentId))!;
        var stale = (await second.Appointments.GetByIdAsync(scenario.AppointmentId))!;
        a.Status = AppointmentStatus.Cancelled;
        await first.SaveChangesAsync();
        stale.Date = scenario.BookingDate;
        var conflict = await Assert.ThrowsAsync<PersistenceConflictException>(() => second.SaveChangesAsync());
        Assert.Equal(PersistenceConflictKind.ConcurrentChange, conflict.Kind);
        var persisted = (await second.Appointments.GetByIdAsync(scenario.AppointmentId))!;
        Assert.NotSame(stale, persisted);
        Assert.Equal(AppointmentStatus.Cancelled, persisted.Status);
        Assert.Equal(scenario.BookingDate.AddDays(1), persisted.Date);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_RejectsOffGridOverlap_ForDoctorAndPatient(bool sameDoctor)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!sameDoctor)
        {
            var other = await db.Appointments.SingleAsync(a => a.Id == scenario.OtherAppointmentId);
            other.Date = other.Date.AddHours(1);
            await db.SaveChangesAsync();
        }
        db.Appointments.Add(new Appointment { DoctorId = sameDoctor ? scenario.DoctorId : scenario.OtherDoctorId,
            PatientId = sameDoctor ? scenario.OtherPatientId : scenario.PatientId,
            Date = scenario.BookingDate.AddDays(1).AddMinutes(15) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Appointments.Add(new Appointment { DoctorId = scenario.DoctorId, PatientId = scenario.PatientId,
            Date = scenario.BookingDate.AddDays(1).AddMinutes(30) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Database_RejectsScheduleOverlap_ButAllowsAdjacentShifts()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
        await unit.DoctorSchedules.AddAsync(new DoctorSchedule { DoctorId = scenario.DoctorId, StartTime = scenario.BookingDate.AddHours(3), EndTime = scenario.BookingDate.AddHours(5) });
        var error = await Assert.ThrowsAsync<PersistenceConflictException>(() => unit.SaveChangesAsync());
        Assert.Equal(PersistenceConflictKind.ScheduleOverlap, error.Kind);
        await unit.DoctorSchedules.AddAsync(new DoctorSchedule { DoctorId = scenario.DoctorId, StartTime = scenario.BookingDate.AddHours(4), EndTime = scenario.BookingDate.AddHours(5) });
        await unit.SaveChangesAsync();
    }

    [Fact]
    public async Task Database_EnforcesOneReviewPerPatientAndDoctor()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().Create();
        await unit.Reviews.AddAsync(new Review { PatientId = scenario.OtherPatientId, DoctorId = scenario.OtherDoctorId, Rating = 4 });
        var error = await Assert.ThrowsAsync<PersistenceConflictException>(() => unit.SaveChangesAsync());
        Assert.Equal(PersistenceConflictKind.DuplicateReview, error.Kind);
    }
}

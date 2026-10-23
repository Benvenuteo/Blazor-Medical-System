using System.Linq.Expressions;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.UnitTests.Support;
using Moq;

namespace MedicalBookingSystem.UnitTests.Services;

public sealed class AppointmentLifecycleTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Completion_IsAllowedOnlyAfterWholeVisitEnds(int secondsAfterEnd, bool expected)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var appointment = new Appointment { Id = 7, DoctorId = 1, PatientId = 2, Date = clock.Now.AddMinutes(-30).AddSeconds(secondsAfterEnd) };
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(appointment);
        var service = TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Doctor(1));
        var result = await service.CompleteAppointmentAsync(7);
        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? AppointmentStatus.Completed : AppointmentStatus.Scheduled, appointment.Status);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), expected ? Times.Once : Times.Never);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task TerminalStates_CannotBeCancelledRescheduledOrCompletedAgain(AppointmentStatus status)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var visit = new Appointment { Id = 7, PatientId = 2, DoctorId = 1, Date = clock.Now.AddDays(2), Status = status };
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(visit);
        var patient = TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Patient());
        Assert.Equal(ErrorType.Conflict, (await patient.CancelAppointmentAsync(7, 2)).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await patient.UpdateAppointmentAsync(7, new UpdateAppointmentDto { NewDate = clock.Now.AddDays(3) })).ErrorType);
        var doctor = TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Doctor(1));
        Assert.Equal(ErrorType.Conflict, (await doctor.CompleteAppointmentAsync(7)).ErrorType);
        Assert.Equal(status, visit.Status);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Reschedule_ExcludesOwnVisit_AndPersistsTheNewSlot()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var visit = new Appointment { Id = 7, PatientId = 2, DoctorId = 1, Date = clock.Now.AddDays(2) };
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(visit);
        var next = visit.Date.AddHours(1);
        repositories.Schedules.Setup(r => r.IsTimeSlotAvailableAsync(1, next, 7)).ReturnsAsync(true);
        var result = await TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Patient()).UpdateAppointmentAsync(7, new UpdateAppointmentDto { NewDate = next });
        Assert.True(result.IsSuccess);
        Assert.Equal(next, visit.Date);
        repositories.Schedules.Verify(r => r.IsTimeSlotAvailableAsync(1, next, 7), Times.Once);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Reschedule_Within24HoursOfOriginalVisit_IsRejectedEvenForDistantNewDate()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var visit = new Appointment { Id = 7, PatientId = 2, DoctorId = 1, Date = clock.Now.AddHours(23) };
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(visit);
        var result = await TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Patient()).UpdateAppointmentAsync(7, new UpdateAppointmentDto { NewDate = clock.Now.AddDays(3) });
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        repositories.Schedules.Verify(r => r.IsTimeSlotAvailableAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Never);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_ValidatesDirectServiceCalls_BeforeDatabaseWork()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var result = await TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Patient()).CreateAppointmentAsync(new CreateAppointmentDto { DoctorId = 1, Date = clock.Now.AddDays(2).AddMinutes(1) }, 2);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        repositories.Doctors.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repositories.Appointments.Verify(r => r.AddAsync(It.IsAny<Appointment>()), Times.Never);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task MedicalDocuments_RequireCompletedVisit(AppointmentStatus status)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(new Appointment { Id = 7, DoctorId = 1, PatientId = 2, Date = clock.Now.AddDays(-1), Status = status });
        var service = TestServices.Records(repositories.UnitOfWork.Object, clock, TestUsers.Doctor(1));
        var note = await service.AddNoteToAppointmentAsync(new CreateNoteDto { AppointmentId = 7, Content = "Valid clinical note" }, 1);
        var prescription = await service.CreatePrescriptionAsync(new CreatePrescriptionDto { AppointmentId = 7, Medication = "Test", Dosage = "Daily", ValidityDays = 7 }, 1);
        Assert.Equal(ErrorType.Conflict, note.ErrorType);
        Assert.Equal(ErrorType.Conflict, prescription.ErrorType);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task MedicalDocuments_UseSharedValidationForDirectServerCalls()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        repositories.Appointments.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(new Appointment { Id = 7, DoctorId = 1, Date = clock.Now.AddDays(-1), Status = AppointmentStatus.Completed });
        var service = TestServices.Records(repositories.UnitOfWork.Object, clock, TestUsers.Doctor(1));
        Assert.Equal(ErrorType.Validation, (await service.AddNoteToAppointmentAsync(new CreateNoteDto { AppointmentId = 7, Content = "short" }, 1)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.AddNoteToAppointmentAsync(new CreateNoteDto { AppointmentId = 7, Content = "          short          " }, 1)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreatePrescriptionAsync(new CreatePrescriptionDto { AppointmentId = 7, Medication = "Test", Dosage = "Daily", ValidityDays = 0 }, 1)).ErrorType);
        repositories.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task PersistenceCollision_IsReturnedAsConflict_WithoutLeakingDatabaseDetails()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        repositories.Doctors.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Doctor { Id = 1 });
        repositories.Schedules.Setup(r => r.IsTimeSlotAvailableAsync(1, clock.Now.AddDays(2), null)).ReturnsAsync(true);
        repositories.UnitOfWork.Setup(u => u.SaveChangesAsync()).ThrowsAsync(new PersistenceConflictException(PersistenceConflictKind.AppointmentSlot, new Exception("SQL internal details")));
        var result = await TestServices.Appointments(repositories.UnitOfWork.Object, clock, TestUsers.Patient()).CreateAppointmentAsync(new CreateAppointmentDto { DoctorId = 1, Date = clock.Now.AddDays(2) }, 2);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.DoesNotContain("SQL", result.Error);
    }
}

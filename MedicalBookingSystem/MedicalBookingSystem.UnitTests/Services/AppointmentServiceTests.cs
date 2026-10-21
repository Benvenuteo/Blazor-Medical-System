using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.UnitTests.Support;
using Moq;

namespace MedicalBookingSystem.UnitTests.Services;

public sealed class AppointmentServiceTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task CancelAppointment_EnforcesInclusive24HourBoundary(int secondsFromBoundary, bool expected)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var appointment = new Appointment
        {
            Id = 7, PatientId = 2, Date = clock.Now.AddHours(24).AddSeconds(secondsFromBoundary),
            Status = AppointmentStatus.Scheduled
        };
        repositories.Appointments.Setup(repository => repository.GetByIdAsync(7)).ReturnsAsync(appointment);
        var service = new AppointmentService(repositories.UnitOfWork.Object, clock, TestUsers.Patient());

        var result = await service.CancelAppointmentAsync(7, 2);

        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? AppointmentStatus.Cancelled : AppointmentStatus.Scheduled, appointment.Status);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), expected ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task CancelAppointment_ForAnotherPatient_DoesNotChangeState()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var appointment = new Appointment { Id = 7, PatientId = 2, Date = clock.Now.AddDays(2) };
        repositories.Appointments.Setup(repository => repository.GetByIdAsync(7)).ReturnsAsync(appointment);
        var service = new AppointmentService(repositories.UnitOfWork.Object, clock, TestUsers.Patient());

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.CancelAppointmentAsync(7, 3));
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelAppointment_WhenAlreadyCancelled_DoesNotSaveAgain()
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        repositories.Appointments.Setup(repository => repository.GetByIdAsync(7)).ReturnsAsync(new Appointment
        {
            Id = 7, PatientId = 2, Date = clock.Now.AddDays(2), Status = AppointmentStatus.Cancelled
        });
        var service = new AppointmentService(repositories.UnitOfWork.Object, clock, TestUsers.Patient());

        var result = await service.CancelAppointmentAsync(7, 2);

        Assert.True(result.IsFailure);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }
}

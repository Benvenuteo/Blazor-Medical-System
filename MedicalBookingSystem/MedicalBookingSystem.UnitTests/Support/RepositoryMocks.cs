using MedicalBookingSystem.Domain.Contracts;
using Moq;

namespace MedicalBookingSystem.UnitTests.Support;

internal sealed class RepositoryMocks
{
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IPatientRepository> Patients { get; } = new();
    public Mock<IDoctorRepository> Doctors { get; } = new();
    public Mock<IDoctorScheduleRepository> Schedules { get; } = new();
    public Mock<IAppointmentRepository> Appointments { get; } = new();

    public RepositoryMocks()
    {
        UnitOfWork.SetupGet(unit => unit.Patients).Returns(Patients.Object);
        UnitOfWork.SetupGet(unit => unit.Doctors).Returns(Doctors.Object);
        UnitOfWork.SetupGet(unit => unit.DoctorSchedules).Returns(Schedules.Object);
        UnitOfWork.SetupGet(unit => unit.Appointments).Returns(Appointments.Object);
        UnitOfWork.Setup(unit => unit.SaveChangesAsync()).ReturnsAsync(1);
    }
}

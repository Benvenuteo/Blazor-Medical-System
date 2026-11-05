using MedicalBookingSystem.Domain.Models;
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
    public Mock<INoteRepository> Notes { get; } = new();
    public Mock<IPrescriptionRepository> Prescriptions { get; } = new();
    public Mock<IReviewRepository> Reviews { get; } = new();
    public Mock<IRepository<DoctorClinic>> DoctorClinics { get; } = new();
    public Mock<IRepository<Clinic>> Clinics { get; } = new();

    public RepositoryMocks()
    {
        UnitOfWork.SetupGet(unit => unit.ClinicalAudit).Returns(new Mock<IClinicalAuditWriter>().Object);
        UnitOfWork.SetupGet(unit => unit.Patients).Returns(Patients.Object);
        UnitOfWork.SetupGet(unit => unit.Doctors).Returns(Doctors.Object);
        UnitOfWork.SetupGet(unit => unit.DoctorSchedules).Returns(Schedules.Object);
        UnitOfWork.SetupGet(unit => unit.Appointments).Returns(Appointments.Object);
        UnitOfWork.SetupGet(unit => unit.Notes).Returns(Notes.Object);
        UnitOfWork.SetupGet(unit => unit.Prescriptions).Returns(Prescriptions.Object);
        UnitOfWork.SetupGet(unit => unit.Reviews).Returns(Reviews.Object);
        // No clinics by default: schedules and slots behave as before clinics existed.
        DoctorClinics.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DoctorClinic, bool>>>())).ReturnsAsync([]);
        Clinics.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinic, bool>>>())).ReturnsAsync([]);
        UnitOfWork.SetupGet(unit => unit.DoctorClinics).Returns(DoctorClinics.Object);
        UnitOfWork.SetupGet(unit => unit.Clinics).Returns(Clinics.Object);
        UnitOfWork.Setup(unit => unit.SaveChangesAsync()).ReturnsAsync(1);
    }
}

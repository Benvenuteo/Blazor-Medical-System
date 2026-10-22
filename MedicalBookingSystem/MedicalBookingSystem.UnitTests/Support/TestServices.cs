using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Contracts;

namespace MedicalBookingSystem.UnitTests.Support;

internal static class TestServices
{
    public static PatientService Patients(IUnitOfWork unit, Microsoft.AspNetCore.Identity.IPasswordHasher<MedicalBookingSystem.Domain.Models.Patient> hasher, UserAccess access) =>
        new(new TestUnitOfWorkFactory(unit), hasher, access, new UpdatePatientDtoValidator());
    public static AppointmentService Appointments(IUnitOfWork uow, ClinicClock clock, UserAccess access) =>
        new(new TestUnitOfWorkFactory(uow), clock, access, new CreateAppointmentDtoValidator(clock), new UpdateAppointmentDtoValidator(clock));
    public static MedicalRecordService Records(IUnitOfWork uow, ClinicClock clock, UserAccess access) =>
        new(new TestUnitOfWorkFactory(uow), clock, access, new CreateNoteDtoValidator(), new CreatePrescriptionDtoValidator());
}

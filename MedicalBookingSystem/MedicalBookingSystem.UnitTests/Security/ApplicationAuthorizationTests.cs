using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.UnitTests.Support;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedicalBookingSystem.UnitTests.Security;

public sealed class ApplicationAuthorizationTests
{
    [Fact]
    public async Task PatientService_RejectsSpoofedIdentity_BeforeReadingOrWritingDatabase()
    {
        var repositories = new RepositoryMocks();
        var service = new PatientService(repositories.UnitOfWork.Object, new PasswordHasher<Patient>(), TestUsers.Patient(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.UpdatePatientAsync(2, new UpdatePatientDto { FirstName = "Changed" }));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetPatientProfileAsync(2));
        repositories.Patients.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repositories.UnitOfWork.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ScheduleManagement_RejectsAnotherDoctor_BeforeValidationAndMutation()
    {
        var repositories = new RepositoryMocks();
        var validator = new Mock<FluentValidation.IValidator<CreateScheduleDto>>(MockBehavior.Strict);
        var service = new DoctorService(repositories.UnitOfWork.Object, validator.Object, new TestTimeProvider().CreateClock(), TestUsers.Doctor(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.CreateScheduleAsync(new CreateScheduleDto { DoctorId = 2 }));
        repositories.Schedules.Verify(r => r.AddAsync(It.IsAny<DoctorSchedule>()), Times.Never);
        repositories.UnitOfWork.Verify(r => r.SaveChangesAsync(), Times.Never);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ClinicalWrite_RejectsForeignAppointment_EvenWhenCallerPassesOwnDoctorId()
    {
        var repositories = new RepositoryMocks();
        repositories.Appointments.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new Appointment { Id = 3, DoctorId = 2, PatientId = 8 });
        var service = new MedicalRecordService(repositories.UnitOfWork.Object, new TestTimeProvider().CreateClock(), TestUsers.Doctor(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.AddNoteToAppointmentAsync(new CreateNoteDto { AppointmentId = 3, Content = "Clinical note" }, 1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.CreatePrescriptionAsync(new CreatePrescriptionDto { AppointmentId = 3 }, 1));
        repositories.UnitOfWork.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExpiredCircuitIdentity_CannotUseApplicationService()
    {
        var time = new TestTimeProvider();
        var clock = time.CreateClock();
        var principal = MedicalBookingSystem.SharedKernel.Security.AccountClaims.Create(1, "Test", "test@example.test", "Patient", new DateTimeOffset(clock.UtcNow).AddHours(-1));
        var access = new UserAccess(new TestUsers(principal), clock);
        var error = await Assert.ThrowsAsync<AccessDeniedException>(() => access.RequirePatientAsync(1));
        Assert.False(error.Authenticated);
    }

    [Fact]
    public async Task AdminRole_DoesNotGrantClinicalOrPatientPermissions()
    {
        var access = TestUsers.Admin();
        await access.RequireAdminAsync();
        await access.RequireScheduleManagementAsync(42);
        await Assert.ThrowsAsync<AccessDeniedException>(() => access.RequireDoctorAsync(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => access.RequirePatientAsync(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => access.RequireMedicalRecordAsync(1, 1));
    }
}

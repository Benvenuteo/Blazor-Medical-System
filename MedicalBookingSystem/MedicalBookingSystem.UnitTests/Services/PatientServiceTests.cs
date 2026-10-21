using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.UnitTests.Support;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedicalBookingSystem.UnitTests.Services;

public sealed class PatientServiceTests
{
    [Theory]
    [InlineData("incorrect")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UpdatePassword_WithInvalidCurrentPassword_DoesNotMutateOrSavePatient(string? currentPassword)
    {
        var repositories = new RepositoryMocks();
        var hasher = new PasswordHasher<Patient>();
        var patient = CreatePatient(hasher);
        var originalHash = patient.PasswordHash;
        repositories.Patients.Setup(repository => repository.GetByIdAsync(patient.Id)).ReturnsAsync(patient);
        var service = new PatientService(repositories.UnitOfWork.Object, hasher, TestUsers.Patient(7));

        var result = await service.UpdatePatientAsync(patient.Id, new UpdatePatientDto
        {
            FirstName = "Changed", CurrentPassword = currentPassword!, NewPassword = "Replacement2026!"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Jan", patient.FirstName);
        Assert.Equal(originalHash, patient.PasswordHash);
        repositories.Patients.Verify(repository => repository.Update(It.IsAny<Patient>()), Times.Never);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdatePassword_WithCorrectCurrentPassword_PersistsNewHash()
    {
        var repositories = new RepositoryMocks();
        var hasher = new PasswordHasher<Patient>();
        var patient = CreatePatient(hasher);
        var originalHash = patient.PasswordHash;
        repositories.Patients.Setup(repository => repository.GetByIdAsync(patient.Id)).ReturnsAsync(patient);
        var service = new PatientService(repositories.UnitOfWork.Object, hasher, TestUsers.Patient(7));

        var result = await service.UpdatePatientAsync(patient.Id, new UpdatePatientDto
        {
            CurrentPassword = "Original2026!", NewPassword = "Replacement2026!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotEqual(originalHash, patient.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(patient, patient.PasswordHash, "Replacement2026!"));
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_WithoutPasswordChange_DoesNotRequireCurrentPassword()
    {
        var repositories = new RepositoryMocks();
        var hasher = new PasswordHasher<Patient>();
        var patient = CreatePatient(hasher);
        var originalHash = patient.PasswordHash;
        repositories.Patients.Setup(repository => repository.GetByIdAsync(patient.Id)).ReturnsAsync(patient);
        var service = new PatientService(repositories.UnitOfWork.Object, hasher, TestUsers.Patient(7));

        var result = await service.UpdatePatientAsync(patient.Id, new UpdatePatientDto { FirstName = "Adam" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Adam", patient.FirstName);
        Assert.Equal(originalHash, patient.PasswordHash);
    }

    [Fact]
    public async Task UpdateProfile_WhenPatientDoesNotExist_DoesNotSave()
    {
        var repositories = new RepositoryMocks();
        var service = new PatientService(repositories.UnitOfWork.Object, new PasswordHasher<Patient>(), TestUsers.Patient(404));

        var result = await service.UpdatePatientAsync(404, new UpdatePatientDto());

        Assert.True(result.IsFailure);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    private static Patient CreatePatient(IPasswordHasher<Patient> hasher)
    {
        var patient = new Patient { Id = 7, FirstName = "Jan", LastName = "Testowy", Email = "patient@example.test" };
        patient.PasswordHash = hasher.HashPassword(patient, "Original2026!");
        return patient;
    }
}

using FluentValidation;
using FluentValidation.Results;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.UnitTests.Support;
using Moq;

namespace MedicalBookingSystem.UnitTests.Services;

public sealed class DoctorServiceTests
{
    [Fact]
    public async Task CreateSchedule_WaitsForValidationAndRejectsInvalidInput()
    {
        var repositories = new RepositoryMocks();
        var completion = new TaskCompletionSource<ValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var validator = new Mock<IValidator<CreateScheduleDto>>();
        validator.Setup(item => item.ValidateAsync(It.IsAny<CreateScheduleDto>(), It.IsAny<CancellationToken>()))
            .Returns(completion.Task);
        var service = new DoctorService(repositories.UnitOfWork.Object, validator.Object, new TestTimeProvider().CreateClock());

        var operation = service.CreateScheduleAsync(new CreateScheduleDto());
        Assert.False(operation.IsCompleted);
        completion.SetResult(new ValidationResult([new ValidationFailure("StartTime", "Invalid interval")]));
        var result = await operation;

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid interval", result.Error);
        repositories.Schedules.Verify(repository => repository.AddAsync(It.IsAny<DoctorSchedule>()), Times.Never);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateSchedule_DoesNotSaveBeforeAddingTheScheduleCompletes()
    {
        var repositories = new RepositoryMocks();
        repositories.Doctors.Setup(repository => repository.GetByIdAsync(1)).ReturnsAsync(new Doctor { Id = 1 });
        var addition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repositories.Schedules.Setup(repository => repository.AddAsync(It.IsAny<DoctorSchedule>())).Returns(addition.Task);
        var service = CreateService(repositories);

        var operation = service.CreateScheduleAsync(new CreateScheduleDto { DoctorId = 1 });
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
        addition.SetResult();
        var result = await operation;

        Assert.True(result.IsSuccess);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateSchedule_WhenDoctorDoesNotExist_DoesNotSave()
    {
        var repositories = new RepositoryMocks();
        var result = await CreateService(repositories).CreateScheduleAsync(new CreateScheduleDto { DoctorId = 404 });

        Assert.True(result.IsFailure);
        repositories.UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SearchDoctors_SortsByRatingWithoutRequiringRegion()
    {
        var repositories = new RepositoryMocks();
        var lowRated = new Doctor { Id = 1, Reviews = [new Review { Rating = 2 }] };
        var highRated = new Doctor { Id = 2, Reviews = [new Review { Rating = 5 }] };
        lowRated.UpdateAverageRating();
        highRated.UpdateAverageRating();
        repositories.Doctors.Setup(repository => repository.GetAllAsync()).ReturnsAsync([lowRated, highRated]);

        var result = await CreateService(repositories).SearchDoctorsAsync(new SearchDoctorsDto { SortBy = "rating" });

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 1], result.Value.Select(doctor => doctor.Id));
    }

    private static DoctorService CreateService(RepositoryMocks repositories)
    {
        var validator = new Mock<IValidator<CreateScheduleDto>>();
        validator.Setup(item => item.ValidateAsync(It.IsAny<CreateScheduleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        return new DoctorService(repositories.UnitOfWork.Object, validator.Object, new TestTimeProvider().CreateClock());
    }
}

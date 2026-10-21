using Moq;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.UnitTests.Support;

namespace MedicalBookingSystem.UnitTests.Validators;

public sealed class ScheduleValidationTests
{
    [Theory]
    [InlineData(7, 8, true)]
    [InlineData(8, 9, false)]
    [InlineData(8, 10, false)]
    [InlineData(9, 10, false)]
    [InlineData(9, 11, false)]
    [InlineData(10, 11, true)]
    [InlineData(7, 11, false)]
    public async Task ScheduleIntervals_AllowTouchingBoundariesButRejectOverlaps(int startHour, int endHour, bool expected)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var tomorrow = clock.Today.AddDays(1);
        repositories.Schedules.Setup(repository => repository.GetByDoctorIdAsync(1)).ReturnsAsync([
            new DoctorSchedule { DoctorId = 1, StartTime = tomorrow.AddHours(8), EndTime = tomorrow.AddHours(10) }
        ]);
        var validator = new CreateScheduleDtoValidator(repositories.UnitOfWork.Object, clock);

        var result = await validator.ValidateAsync(new CreateScheduleDto
        {
            DoctorId = 1, StartTime = tomorrow.AddHours(startHour), EndTime = tomorrow.AddHours(endHour)
        });

        Assert.Equal(expected, result.IsValid);
    }
}

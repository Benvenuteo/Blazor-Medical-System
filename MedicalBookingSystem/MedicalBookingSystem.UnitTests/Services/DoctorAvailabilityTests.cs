using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.UnitTests.Support;
using Moq;

namespace MedicalBookingSystem.UnitTests.Services;

public sealed class DoctorAvailabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvailabilitySort_UsesFirstFreeSlot_InsteadOfShiftStart_WithOrWithoutDate(bool filterDate)
    {
        var repositories = new RepositoryMocks();
        var clock = new TestTimeProvider().CreateClock();
        var day = clock.Today.AddDays(2);
        var booked = new Doctor { Id = 1, Schedules = [new DoctorSchedule { StartTime = day.AddHours(9), EndTime = day.AddHours(11) }],
            Appointments = [new Appointment { Date = day.AddHours(9) }, new Appointment { Date = day.AddHours(9.5) }] };
        var earlier = new Doctor { Id = 2, Schedules = [new DoctorSchedule { StartTime = day.AddHours(9.5), EndTime = day.AddHours(11) }] };
        var unavailable = new Doctor { Id = 3 };
        repositories.Doctors.Setup(r => r.GetAllAsync()).ReturnsAsync([booked, unavailable, earlier]);
        var service = new DoctorService(repositories.UnitOfWork.Object, new CreateScheduleDtoValidator(repositories.UnitOfWork.Object, clock), clock, TestUsers.Admin());
        var result = await service.SearchDoctorsAsync(new SearchDoctorsDto { Date = filterDate ? day : null, SortBy = "availability" });
        var doctors = result.Value.ToArray();
        Assert.Equal(filterDate ? new[] { 2, 1 } : new[] { 2, 1, 3 }, doctors.Select(d => d.Id));
        Assert.Equal(day.AddHours(9.5), doctors[0].NextAvailableSlot);
        Assert.Equal(day.AddHours(10), doctors[1].NextAvailableSlot);
    }
}

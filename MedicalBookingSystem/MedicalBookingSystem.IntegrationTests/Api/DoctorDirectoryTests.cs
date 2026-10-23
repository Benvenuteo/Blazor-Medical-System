using System.Net.Http.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class DoctorDirectoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Availability_FilterAndOrderRunBeforePage_AndUseFirstFreeSlot(bool filterDate)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        var day = scenario.BookingDate.Date;
        int laterId, earlierId, unavailableId;
        using (var scope = scenario.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var later = NewDoctor("D-LATER");
            var earlier = NewDoctor("D-EARLIER");
            var unavailable = NewDoctor("D-UNAVAILABLE");
            later.Schedules.Add(new DoctorSchedule { StartTime = day.AddHours(9), EndTime = day.AddHours(11) });
            earlier.Schedules.Add(new DoctorSchedule { StartTime = day.AddHours(9.5), EndTime = day.AddHours(11) });
            later.Appointments.Add(new Appointment { PatientId = scenario.PatientId, Date = day.AddHours(9) });
            later.Appointments.Add(new Appointment { PatientId = scenario.PatientId, Date = day.AddHours(9.5) });
            later.Reviews.Add(new Review { PatientId = scenario.PatientId, Rating = 2 });
            earlier.Reviews.Add(new Review { PatientId = scenario.PatientId, Rating = 5 });
            db.Doctors.AddRange(later, earlier, unavailable);
            await db.SaveChangesAsync();
            laterId = later.Id; earlierId = earlier.Id; unavailableId = unavailable.Id;
        }
        using var client = scenario.Factory.CreateClient();
        var path = $"/api/doctors?Region={Uri.EscapeDataString("łÓdZkIe")}&SortBy=availability&PageSize=1" +
            (filterDate ? $"&Date={day:yyyy-MM-dd}" : "");
        var first = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>(path))!;
        var second = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>(path + "&Page=2"))!;
        Assert.Equal(filterDate ? 2 : 3, first.TotalCount);
        Assert.Equal(earlierId, Assert.Single(first.Items).Id);
        Assert.Equal(day.AddHours(9.5), first.Items[0].NextAvailableSlot);
        Assert.Equal(laterId, Assert.Single(second.Items).Id);
        Assert.Equal(day.AddHours(10), second.Items[0].NextAvailableSlot);
        var last = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>(path + "&Page=3"))!;
        if (filterDate) Assert.Empty(last.Items);
        else { Assert.Equal(unavailableId, Assert.Single(last.Items).Id); Assert.Null(last.Items[0].NextAvailableSlot); }
        var ratings = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>(path.Replace("availability", "rating")))!;
        Assert.Equal(earlierId, Assert.Single(ratings.Items).Id);
        Assert.Equal(5, ratings.Items[0].AverageRating);
        Assert.Equal(1, ratings.Items[0].ReviewCount);
        var beyond = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>(path + "&Page=500"))!;
        Assert.Equal(first.TotalCount, beyond.TotalCount);
        Assert.Empty(beyond.Items);
    }

    [Fact]
    public async Task RegionFilter_IsParameterized_AndUnknownSpecializationReturnsEmptyPage()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        foreach (var query in new[] { "Region=" + Uri.EscapeDataString("' OR 1=1 --"), "SpecializationId=99999" })
        {
            var page = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>("/api/doctors?" + query))!;
            Assert.Equal(0, page.TotalCount);
            Assert.Empty(page.Items);
        }
    }

    [Theory]
    [InlineData(0, 0, 120, -1, false, true, 0)]
    [InlineData(0, 1, 120, -1, false, true, 0)]
    [InlineData(15, 0, 120, -1, false, true, 0)]
    [InlineData(0, 1, 30, -1, false, true, 0)]
    [InlineData(0, 0, 120, 15, false, true, 0)]
    [InlineData(0, 0, 120, 0, true, true, 0)]
    [InlineData(0, 0, 120, -1, false, false, 0)]
    [InlineData(885, 0, 120, -1, false, true, 1)]
    public async Task SqlAvailability_AgreesWithSlotPlanner_ForFractionalTimesOverlapsAndOvernightShifts(
        int startMinutes, int ticks, int lengthMinutes, int busyMinutes, bool cancelled, bool available, int dayOffset)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        int doctorId;
        var origin = scenario.BookingDate.Date.AddHours(9);
        using (var scope = scenario.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = NewDoctor("PARITY"); doctor.Region = "Parity";
            var start = origin.AddMinutes(startMinutes).AddTicks(ticks);
            doctor.Schedules.Add(new DoctorSchedule { StartTime = start, EndTime = start.AddMinutes(lengthMinutes), IsAvailable = available });
            if (busyMinutes >= 0) doctor.Appointments.Add(new Appointment { PatientId = scenario.PatientId, Date = origin.AddMinutes(busyMinutes),
                Status = cancelled ? AppointmentStatus.Cancelled : AppointmentStatus.Scheduled });
            db.Doctors.Add(doctor); await db.SaveChangesAsync(); doctorId = doctor.Id;
        }
        using var client = scenario.Factory.CreateClient();
        var day = origin.Date.AddDays(dayOffset).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var slots = (await client.GetFromJsonAsync<List<MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto.AvailableSlotDto>>($"/api/doctors/{doctorId}/slots?date={day}"))!;
        var page = (await client.GetFromJsonAsync<PagedResult<DoctorDto>>($"/api/doctors?Region=Parity&SortBy=availability&Date={day}"))!;
        if (slots.Count == 0) Assert.Empty(page.Items);
        else Assert.Equal(slots[0].StartTime, Assert.Single(page.Items).NextAvailableSlot);
    }

    private static Doctor NewDoctor(string license) => new() { FirstName = "Anna", LastName = license, LicenseNumber = license, Region = "Łódzkie" };
}

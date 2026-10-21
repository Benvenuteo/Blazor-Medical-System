using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AvailabilityTests
{
    [Fact]
    public async Task PublicSlots_AreWholeFreeHalfHours_AndSearchUsesSameAvailability()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var patient = await scenario.SignInAsync();
        using var booking = await patient.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        using var anonymous = scenario.Factory.CreateClient();
        var date = scenario.BookingDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var slots = await anonymous.GetFromJsonAsync<List<AvailableSlotDto>>($"/api/doctors/{scenario.DoctorId}/slots?date={date}");
        Assert.NotNull(slots);
        Assert.Equal(7, slots.Count);
        Assert.Equal(scenario.BookingDate.AddMinutes(30), slots[0].StartTime);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(30), slot.EndTime - slot.StartTime));
        Assert.Equal(scenario.BookingDate.AddHours(4), slots[^1].EndTime);
        var doctors = await anonymous.GetFromJsonAsync<List<DoctorDto>>($"/api/doctors?Date={date}&SortBy=availability");
        Assert.NotNull(doctors);
        var doctor = Assert.Single(doctors);
        Assert.Equal(scenario.DoctorId, doctor.Id);
        Assert.Equal(slots[0].StartTime, doctor.NextAvailableSlot);
        var noScheduleDay = scenario.BookingDate.AddDays(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Empty((await anonymous.GetFromJsonAsync<List<DoctorDto>>($"/api/doctors?Date={noScheduleDay}"))!);
    }

    [Fact]
    public async Task SlotExclusion_RequiresOwnership_AndSameDateUpdateIsIdempotent()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var appointment = await db.Appointments.SingleAsync(a => a.Id == scenario.AppointmentId);
            appointment.Date = scenario.BookingDate;
            await db.SaveChangesAsync();
        }
        using var owner = await scenario.SignInAsync();
        using var other = await scenario.SignInAsync("two@example.test");
        var path = $"/api/doctors/{scenario.DoctorId}/slots?excludedAppointmentId={scenario.AppointmentId}";
        var slots = await owner.GetFromJsonAsync<List<AvailableSlotDto>>(path);
        Assert.Contains(slots!, slot => slot.StartTime == scenario.BookingDate);
        using var forbidden = await other.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var noChange = await owner.PutAsJsonAsync($"/api/appointments/{scenario.AppointmentId}", new { newDate = scenario.BookingDate, status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, noChange.StatusCode);
        await using var check = scenario.Factory.Services.CreateAsyncScope();
        Assert.Equal(AppointmentStatus.Scheduled, (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Appointments.SingleAsync(a => a.Id == scenario.AppointmentId)).Status);
    }

    [Fact]
    public async Task AuthenticatedSlots_ExcludePatientsOtherVisits_WithoutChangingPublicDoctorCalendar()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using (var scope = scenario.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = scenario.OtherDoctorId, StartTime = scenario.BookingDate, EndTime = scenario.BookingDate.AddHours(1) });
            await db.SaveChangesAsync();
        }
        using var owner = await scenario.SignInAsync();
        using var booking = await owner.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        var path = $"/api/doctors/{scenario.OtherDoctorId}/slots";
        var personal = await owner.GetFromJsonAsync<List<AvailableSlotDto>>(path);
        Assert.DoesNotContain(personal!, s => s.StartTime == scenario.BookingDate);
        using var anonymous = scenario.Factory.CreateClient();
        var publicSlots = await anonymous.GetFromJsonAsync<List<AvailableSlotDto>>(path);
        Assert.Contains(publicSlots!, s => s.StartTime == scenario.BookingDate);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(225)]
    [InlineData(240)]
    public async Task Booking_MustFitCompletelyInsideShift_AndUseAlignedTime(int minutes)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var response = await client.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate.AddMinutes(minutes) });
        Assert.Equal(minutes == 225 ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, response.StatusCode);
    }
}

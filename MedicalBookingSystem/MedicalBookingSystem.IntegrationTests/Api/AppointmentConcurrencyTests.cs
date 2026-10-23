using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AppointmentConcurrencyTests
{
    [Fact]
    public async Task ConcurrentBookingOfOneSlot_ExactlyOneRequestSucceeds()
    {
        using var database = new TemporarySqliteDatabase();
        var gate = new ConcurrentSaveGate();
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(database.ConnectionString, interceptor: gate));
        using var first = await scenario.SignInAsync();
        using var second = await scenario.SignInAsync("two@example.test");
        gate.Active = true;
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate }),
            second.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate }));
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.Order(), responses.Select(r => r.StatusCode).Order());
        foreach (var response in responses) response.Dispose();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Appointments.CountAsync(a => a.DoctorId == scenario.DoctorId && a.Date == scenario.BookingDate));
    }

    [Fact]
    public async Task SamePatientBookingTwoDoctorsConcurrently_CannotCreateOverlappingVisits()
    {
        using var database = new TemporarySqliteDatabase();
        var gate = new ConcurrentSaveGate();
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(database.ConnectionString, interceptor: gate));
        await using (var setup = scenario.Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = scenario.OtherDoctorId, StartTime = scenario.BookingDate, EndTime = scenario.BookingDate.AddHours(1) });
            await db.SaveChangesAsync();
        }
        using var client = await scenario.SignInAsync();
        gate.Active = true;
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate }),
            client.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.OtherDoctorId, date = scenario.BookingDate }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task ConcurrentMoveAndBooking_KeepOriginalVisitWhenMoveLoses()
    {
        using var database = new TemporarySqliteDatabase();
        var gate = new ConcurrentSaveGate();
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(database.ConnectionString, interceptor: gate));
        using var owner = await scenario.SignInAsync();
        using var other = await scenario.SignInAsync("two@example.test");
        gate.Active = true;
        var moveTask = owner.PutAsJsonAsync($"/api/appointments/{scenario.AppointmentId}", new { newDate = scenario.BookingDate });
        var bookTask = other.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        await Task.WhenAll(moveTask, bookTask);
        using var move = await moveTask;
        using var booking = await bookTask;
        Assert.True((move.StatusCode == HttpStatusCode.NoContent && booking.StatusCode == HttpStatusCode.Conflict) ||
            (move.StatusCode == HttpStatusCode.Conflict && booking.StatusCode == HttpStatusCode.Created));
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.Appointments.SingleAsync(a => a.Id == scenario.AppointmentId);
        Assert.Equal(move.IsSuccessStatusCode ? scenario.BookingDate : scenario.BookingDate.AddDays(1), original.Date);
        Assert.Equal(AppointmentStatus.Scheduled, original.Status);
        Assert.Equal(1, await db.Appointments.CountAsync(a => a.DoctorId == scenario.DoctorId && a.Date == scenario.BookingDate));
    }

    [Fact]
    public async Task Cancellation_ReleasesSlotForAnotherPatient()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var first = await scenario.SignInAsync();
        using var second = await scenario.SignInAsync("two@example.test");
        using var booking = await first.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        using var body = JsonDocument.Parse(await booking.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetInt32();
        using var conflict = await second.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var cancel = await first.DeleteAsync($"/api/appointments/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        using var reused = await second.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
    }
}

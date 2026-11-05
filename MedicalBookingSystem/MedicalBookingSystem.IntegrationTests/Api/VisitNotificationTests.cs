using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Notifications;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class VisitNotificationTests
{
    [Fact]
    public async Task Booking_QueuesAConfirmationWithACalendarAttachment()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();

        using var booked = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate });

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var mail = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailOutbox.SingleAsync();
        Assert.Equal("one@example.test", mail.To);
        Assert.Equal("Potwierdzenie wizyty", mail.Subject);
        Assert.Equal("wizyta.ics", mail.AttachmentName);
        Assert.Contains("BEGIN:VEVENT", mail.AttachmentContent);
        Assert.Contains("First Doctor", mail.TextBody);
    }

    [Fact]
    public async Task Reminders_AreQueuedOnceForTomorrowsScheduledVisitsOnly()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tomorrow = services.GetRequiredService<ClinicClock>().Today.AddDays(1);
        db.Appointments.AddRange(
            new Appointment { PatientId = scenario.PatientId, DoctorId = scenario.OtherDoctorId, Date = tomorrow.AddHours(9) },
            new Appointment { PatientId = scenario.OtherPatientId, DoctorId = scenario.OtherDoctorId, Date = tomorrow.AddHours(10), Status = AppointmentStatus.Cancelled },
            new Appointment { PatientId = scenario.OtherPatientId, DoctorId = scenario.OtherDoctorId, Date = tomorrow.AddDays(1).AddHours(9) });
        await db.SaveChangesAsync();
        var notifications = services.GetRequiredService<VisitNotificationService>();

        var first = await notifications.QueueTomorrowRemindersAsync();
        var second = await notifications.QueueTomorrowRemindersAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var reminder = await db.EmailOutbox.AsNoTracking().SingleAsync();
        Assert.Equal("Przypomnienie: wizyta jutro", reminder.Subject);
        Assert.Equal("one@example.test", reminder.To);
    }

    [Fact]
    public async Task CalendarFile_IsAvailableToTheOwnerOfAScheduledVisitOnly()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var owner = await scenario.SignInAsync();
        using var other = await scenario.SignInAsync("two@example.test");

        using var file = await owner.GetAsync($"/api/appointments/{scenario.AppointmentId}/calendar");
        using var foreign = await other.GetAsync($"/api/appointments/{scenario.AppointmentId}/calendar");
        using var completed = await owner.GetAsync($"/api/appointments/{scenario.ClinicalAppointmentId}/calendar");

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("text/calendar", file.Content.Headers.ContentType!.MediaType);
        Assert.Contains($"UID:appointment-{scenario.AppointmentId}@", await file.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
    }
}

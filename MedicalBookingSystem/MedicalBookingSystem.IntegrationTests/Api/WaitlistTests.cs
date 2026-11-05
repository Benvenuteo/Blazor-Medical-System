using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class WaitlistTests
{
    [Fact]
    public async Task Joining_ValidatesTheDate_AndAllowsOneOpenEntryPerDoctor()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync("two@example.test");

        using var tooSoon = await client.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = DateTime.Now.AddHours(3) });
        using var tooFar = await client.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = DateTime.Now.AddMonths(4) });
        using var joined = await client.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = scenario.BookingDate.AddDays(5) });
        using var again = await client.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = scenario.BookingDate.AddDays(6) });
        var mine = await client.GetFromJsonAsync<List<WaitlistEntryDto>>("/api/waitlist");
        using var leave = await client.DeleteAsync($"/api/waitlist/{mine!.Single().Id}");

        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("First Doctor", mine!.Single().DoctorName);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<WaitlistEntryDto>>("/api/waitlist"))!);
    }

    [Fact]
    public async Task Cancellation_NotifiesMatchingEntriesOnce_AndClosesThem()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var waiting = await scenario.SignInAsync("two@example.test");
        using var owner = await scenario.SignInAsync();
        // The owner's visit is on BookingDate + 1 day; the waiting patient wants anything before BookingDate + 3 days.
        await waiting.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = scenario.BookingDate.AddDays(3) });

        using var cancel = await owner.DeleteAsync($"/api/appointments/{scenario.AppointmentId}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mail = await db.EmailOutbox.AsNoTracking().SingleAsync();
        Assert.Equal("two@example.test", mail.To);
        Assert.Equal("Zwolnił się wcześniejszy termin", mail.Subject);
        Assert.Contains($"/appointments/create?doctorId={scenario.DoctorId}", mail.TextBody);
        var entry = await db.Waitlist.AsNoTracking().SingleAsync();
        Assert.False(entry.Active);
        Assert.NotNull(entry.NotifiedAtUtc);
        Assert.Empty((await waiting.GetFromJsonAsync<List<WaitlistEntryDto>>("/api/waitlist"))!);
    }

    [Fact]
    public async Task FreedSlotsLaterThanTheWish_AndOtherDoctors_DoNotNotify()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var waiting = await scenario.SignInAsync("two@example.test");
        using var owner = await scenario.SignInAsync();
        // Wants a slot before the freed one (BookingDate + 1 day), and another doctor entirely.
        await waiting.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.DoctorId, Before = scenario.BookingDate.AddHours(1) });
        await waiting.PostAsJsonAsync("/api/waitlist", new JoinWaitlistDto { DoctorId = scenario.OtherDoctorId, Before = scenario.BookingDate.AddDays(10) });

        await owner.DeleteAsync($"/api/appointments/{scenario.AppointmentId}");

        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.EmailOutbox.AnyAsync());
        Assert.Equal(2, await db.Waitlist.CountAsync(e => e.Active));
    }
}

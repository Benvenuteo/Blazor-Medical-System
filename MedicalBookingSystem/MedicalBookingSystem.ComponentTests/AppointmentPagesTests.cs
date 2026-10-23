using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class AppointmentPagesTests : BunitContext
{
    private static readonly DateTime Original = new(2026, 11, 2, 10, 0, 0);
    private static readonly DateTime Alternative = Original.AddMinutes(30);

    [Fact]
    public void BookingConflict_RefreshesAvailableSlots_AndRequiresASecondSelection()
    {
        var handler = new AppointmentApiHandler(conflict: true);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var page = Render<AppointmentsCreate>();
        page.Find("#doctor").Change("1");
        page.WaitForAssertion(() => Assert.Contains("02.11.2026 10:00", page.Markup));
        page.Find("#slot").Change(Original.ToString("s"));
        page.FindAll("button").Single(b => b.TextContent == "Zarezerwuj").Click();
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Termin został zajęty", page.Markup);
            Assert.DoesNotContain("02.11.2026 10:00", page.Markup);
            Assert.Contains("02.11.2026 10:30", page.Markup);
            Assert.True(page.FindAll("button").Single(b => b.TextContent == "Zarezerwuj").HasAttribute("disabled"));
        });
        Assert.Equal(Original, handler.SubmittedDate);
        Assert.Equal(2, handler.SlotRequests);
    }

    [Fact]
    public void Editing_LoadsCurrentVisitAndOwnExcludedSlots_ThenSubmitsOnlyNewDate()
    {
        var handler = new AppointmentApiHandler(conflict: false);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var page = Render<EditAppointment>(p => p.Add(c => c.appointmentId, 7));
        page.WaitForAssertion(() => Assert.Contains("Obecny termin:", page.Markup));
        Assert.Equal("7", handler.ExcludedAppointment);
        page.Find("#new-slot").Change(Alternative.ToString("s"));
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.EndsWith("/appointments", Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(Alternative, handler.SubmittedDate);
        Assert.False(handler.SubmittedStatus);
    }

    private sealed class AppointmentApiHandler(bool conflict) : HttpMessageHandler
    {
        public int SlotRequests { get; private set; }
        public DateTime? SubmittedDate { get; private set; }
        public bool SubmittedStatus { get; private set; }
        public string? ExcludedAppointment { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/doctors")
                return Ok(new { items = new[] { new { id = 1, firstName = "Anna", lastName = "Testowa" } }, totalCount = 1, page = 1, pageSize = 10 });
            if (request.Method == HttpMethod.Get && path.EndsWith("/slots"))
            {
                SlotRequests++;
                if (request.RequestUri.Query.Contains("excludedAppointmentId=7")) ExcludedAppointment = "7";
                var starts = conflict && SlotRequests > 1 ? new[] { Alternative } : new[] { Original, Alternative };
                return Ok(starts.Select(date => new { doctorId = 1, startTime = date, endTime = date.AddMinutes(30) }));
            }
            if (request.Method == HttpMethod.Get && path == "/api/appointments/7")
                return Ok(new { id = 7, doctorId = 1, doctorName = "Anna Testowa", date = Original, status = "Scheduled" });
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            {
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var body = json.RootElement;
                SubmittedDate = body.GetProperty(request.Method == HttpMethod.Post ? "date" : "newDate").GetDateTime();
                SubmittedStatus = body.TryGetProperty("status", out _);
                return conflict ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "Termin został zajęty. Wybierz inny." }) }
                    : Ok(new { });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}

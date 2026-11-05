using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class RemoteVisitTests
{
    [Fact]
    public async Task RemoteVisit_GetsAPrivateVideoLink_InTheListAndTheConfirmation()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();

        using var remote = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate, Remote = true });
        using var inPerson = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate.AddMinutes(30) });
        var upcoming = (await client.GetFromJsonAsync<PagedResult<AppointmentDto>>("/api/appointments/upcoming?pageSize=50"))!.Items;

        Assert.Equal(HttpStatusCode.Created, remote.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inPerson.StatusCode);
        var video = upcoming.Single(a => a.Date == scenario.BookingDate);
        Assert.True(video.IsRemote);
        Assert.Matches("^https://meet\\.jit\\.si/MedicalBooking-[0-9a-f]{24}$", video.VideoLink);
        var clinic = upcoming.Single(a => a.Date == scenario.BookingDate.AddMinutes(30));
        Assert.False(clinic.IsRemote);
        Assert.Null(clinic.VideoLink);

        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var confirmation = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailOutbox.AsNoTracking()
            .OrderBy(m => m.Id).FirstAsync();
        Assert.Contains(video.VideoLink!, confirmation.TextBody);
        Assert.Contains("LOCATION:Teleporada", confirmation.AttachmentContent);
    }

    [Fact]
    public async Task EachRemoteVisit_GetsItsOwnRoom()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate, Remote = true });
        await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate.AddHours(1), Remote = true });

        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var rooms = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Appointments
            .Where(a => a.IsRemote).Select(a => a.VideoRoom).ToListAsync();

        Assert.Equal(2, rooms.Count);
        Assert.Equal(2, rooms.Distinct().Count());
    }
}

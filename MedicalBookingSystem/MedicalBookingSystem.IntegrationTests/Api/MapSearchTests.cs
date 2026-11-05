using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static System.FormattableString;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class MapSearchTests
{
    // Kraków and points due north: 0.22483° ≈ 25 km, 0.31476° ≈ 35 km.
    private const double Lat = FakeGeocodingService.KrakowLatitude, Lng = FakeGeocodingService.KrakowLongitude;
    private const double NearLat = Lat + 0.22483, FarLat = Lat + 0.31476;

    [Fact]
    public async Task RadiusSearch_ListsOnlyDoctorsWithAClinicInRange_SortedByDistance()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        var map = await ArrangeAsync(scenario);
        using var client = await scenario.SignInAsync();

        var within30 = await SearchAsync(client, Invariant($"Latitude={Lat}&Longitude={Lng}&RadiusKm=30&SortBy=distance"));
        var within10 = await SearchAsync(client, Invariant($"Latitude={Lat}&Longitude={Lng}&RadiusKm=10"));
        var within40 = await SearchAsync(client, Invariant($"Latitude={Lat}&Longitude={Lng}&RadiusKm=40&SortBy=distance"));

        // The other doctor's nearest clinic is about 1 km away, the doctor's only clinic 25 km away.
        Assert.Equal([scenario.OtherDoctorId, scenario.DoctorId], within30.Items.Select(d => d.Id));
        Assert.Equal("Druga bliska", within30.Items[0].ClinicName);
        Assert.InRange(within30.Items[0].DistanceKm!.Value, 0.5, 1.5);
        var doctor = within30.Items[1];
        Assert.Equal(map.NearId, doctor.ClinicId);
        Assert.InRange(doctor.DistanceKm!.Value, 24.5, 25.5);
        Assert.Equal("Bliska placówka", doctor.ClinicName);
        Assert.Equal([scenario.OtherDoctorId], within10.Items.Select(d => d.Id));
        Assert.Equal(2, within40.TotalCount);
    }

    [Fact]
    public async Task NextAvailableSlot_CountsOnlyShiftsHeldInClinicsWithinTheRadius()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        await ArrangeAsync(scenario);
        using var client = await scenario.SignInAsync();

        var nearby = await SearchAsync(client, Invariant($"Latitude={Lat}&Longitude={Lng}&RadiusKm=30"));
        var everywhere = await SearchAsync(client, "PageSize=50");

        // The other doctor also works nearby, but all of their shifts are 35 km away.
        Assert.Null(nearby.Items.Single(d => d.Id == scenario.OtherDoctorId).NextAvailableSlot);
        Assert.NotNull(nearby.Items.Single(d => d.Id == scenario.DoctorId).NextAvailableSlot);
        Assert.NotNull(everywhere.Items.Single(d => d.Id == scenario.OtherDoctorId).NextAvailableSlot);
    }

    [Fact]
    public async Task Booking_StoresTheClinicOfTheShift_AndSlotsShowIt()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        var map = await ArrangeAsync(scenario);
        using var client = await scenario.SignInAsync();

        var slots = await client.GetFromJsonAsync<List<AvailableSlotDto>>($"/api/doctors/{scenario.DoctorId}/slots");
        using var booked = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentDto { DoctorId = scenario.DoctorId, Date = scenario.BookingDate });
        var upcoming = await client.GetFromJsonAsync<PagedResult<AppointmentDto>>("/api/appointments/upcoming?pageSize=50");

        Assert.All(slots!, s => Assert.Equal("Bliska placówka", s.ClinicName));
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var visit = upcoming!.Items.Single(a => a.Date == scenario.BookingDate);
        Assert.Equal("Bliska placówka", visit.ClinicName);
        Assert.Equal(NearLat, visit.ClinicLatitude!.Value, 5);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Appointments.SingleAsync(a => a.Date == scenario.BookingDate && a.DoctorId == scenario.DoctorId);
        Assert.Equal(map.NearId, stored.ClinicId);
    }

    [Fact]
    public async Task Schedules_RequireOneOfTheDoctorsClinics()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        var map = await ArrangeAsync(scenario);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admin = ScenarioPrincipal.Access(services, await ScenarioPrincipal.StaffAsync(services, "admin@example.test"));
        var doctors = new DoctorService(services.GetRequiredService<IUnitOfWorkFactory>(), services.GetRequiredService<IValidator<CreateScheduleDto>>(),
            services.GetRequiredService<ClinicClock>(), admin, services.GetRequiredService<IDoctorDirectoryQueries>());
        var day = services.GetRequiredService<ClinicClock>().Today.AddDays(20);

        var missing = await doctors.CreateScheduleAsync(new CreateScheduleDto { DoctorId = scenario.DoctorId, StartTime = day.AddHours(8), EndTime = day.AddHours(9) });
        var foreign = await doctors.CreateScheduleAsync(new CreateScheduleDto { DoctorId = scenario.DoctorId, StartTime = day.AddHours(8), EndTime = day.AddHours(9), ClinicId = map.FarId });
        var valid = await doctors.CreateScheduleAsync(new CreateScheduleDto { DoctorId = scenario.DoctorId, StartTime = day.AddHours(8), EndTime = day.AddHours(9), ClinicId = map.NearId });

        Assert.Contains("Wybierz placówkę", missing.Error);
        Assert.Contains("nie jest przypisany", foreign.Error);
        Assert.True(valid.IsSuccess);
    }

    [Fact]
    public async Task ClinicAdministration_ValidatesPin_AndKeepsClinicsWithHistory()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        var map = await ArrangeAsync(scenario);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var clinics = new ClinicService(services.GetRequiredService<IUnitOfWorkFactory>(),
            ScenarioPrincipal.Access(services, await ScenarioPrincipal.StaffAsync(services, "admin@example.test")),
            services.GetRequiredService<IGeocodingService>(), services.GetRequiredService<IValidator<SaveClinicDto>>());

        var withoutPin = await clinics.CreateAsync(new SaveClinicDto { Name = "Bez pinezki", Street = "ul. Testowa 1", City = "Kraków" });
        var created = await clinics.CreateAsync(new SaveClinicDto { Name = "Nowa", Street = "ul. Testowa 1", City = "Kraków", PostalCode = "30-001",
            Latitude = Lat, Longitude = Lng, DoctorIds = [scenario.OtherDoctorId] });
        var duplicate = await clinics.CreateAsync(new SaveClinicDto { Name = "NOWA", Street = "x", City = "y", Latitude = 1, Longitude = 1 });
        var detachWithShifts = await clinics.UpdateAsync(map.NearId, new SaveClinicDto { Name = "Bliska placówka", Street = "ul. Bliska 1", City = "Kraków", Latitude = NearLat, Longitude = Lng });
        var deleteWithShifts = await clinics.DeleteAsync(map.NearId);
        var deleteEmpty = await clinics.DeleteAsync(created.Value.Id);
        var geocoded = await clinics.GeocodeAsync("Rynek, Kraków");
        var unknown = await clinics.GeocodeAsync("nieistniejące miejsce");

        Assert.Contains("położenie", withoutPin.Error);
        Assert.True(created.IsSuccess);
        Assert.Equal(ErrorType.Conflict, duplicate.ErrorType);
        Assert.Equal(ErrorType.Conflict, detachWithShifts.ErrorType);
        Assert.Equal(ErrorType.Conflict, deleteWithShifts.ErrorType);
        Assert.True(deleteEmpty.IsSuccess);
        Assert.Equal(Lat, geocoded.Value.Latitude);
        Assert.Equal(ErrorType.NotFound, unknown.ErrorType);
    }

    [Fact]
    public async Task GeocodeEndpoint_RequiresSignIn_AndInvalidSearchCentresAreRejected()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var anonymous = scenario.Factory.CreateClient();
        using var client = await scenario.SignInAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/locations/geocode?q=Kraków")).StatusCode);
        var point = await client.GetFromJsonAsync<GeoPointDto>("/api/locations/geocode?q=Kraków");
        Assert.Equal(Lat, point!.Latitude);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Invariant($"/api/doctors?Latitude={Lat}"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Invariant($"/api/doctors?Latitude={Lat}&Longitude={Lng}&RadiusKm=500"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/doctors?SortBy=distance")).StatusCode);
    }

    private static async Task<PagedResult<DoctorDto>> SearchAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PagedResult<DoctorDto>>("/api/doctors?" + query))!;

    /// <summary>
    /// Doctor: works at the near clinic (25 km), the existing scenario shift is held there.
    /// Other doctor: works at a second near clinic (no shifts) and the far clinic (35 km) with a shift there.
    /// </summary>
    private static async Task<(int NearId, int FarId)> ArrangeAsync(SecurityScenario scenario)
    {
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var near = new Clinic { Name = "Bliska placówka", Street = "ul. Bliska 1", City = "Kraków", Latitude = NearLat, Longitude = Lng };
        var nearEmpty = new Clinic { Name = "Druga bliska", Street = "ul. Pusta 2", City = "Kraków", Latitude = Lat + 0.01, Longitude = Lng };
        var far = new Clinic { Name = "Daleka placówka", Street = "ul. Daleka 3", City = "Miechów", Latitude = FarLat, Longitude = Lng };
        db.Clinics.AddRange(near, nearEmpty, far);
        await db.SaveChangesAsync();
        db.DoctorClinics.AddRange(new DoctorClinic { DoctorId = scenario.DoctorId, ClinicId = near.Id },
            new DoctorClinic { DoctorId = scenario.OtherDoctorId, ClinicId = nearEmpty.Id },
            new DoctorClinic { DoctorId = scenario.OtherDoctorId, ClinicId = far.Id });
        await db.DoctorSchedules.Where(s => s.DoctorId == scenario.DoctorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ClinicId, near.Id));
        db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = scenario.OtherDoctorId, ClinicId = far.Id,
            StartTime = scenario.BookingDate.AddDays(2), EndTime = scenario.BookingDate.AddDays(2).AddHours(3), IsAvailable = true });
        await db.SaveChangesAsync();
        return (near.Id, far.Id);
    }
}

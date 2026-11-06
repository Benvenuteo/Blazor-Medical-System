using System.Net;
using System.Net.Http.Json;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class DoctorMapTests : BunitContext
{
    private readonly List<string> searches = [];

    public DoctorMapTests()
    {
        Services.AddSingleton(new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("specializations")) return Ok(Array.Empty<object>());
            searches.Add(request.RequestUri.Query);
            return Ok(new { items = Array.Empty<object>(), totalCount = 0, page = 1, pageSize = 10 });
        })) { BaseAddress = new Uri("http://localhost") });
        JSInterop.SetupVoid("medicalMap.showDoctors", _ => true).SetVoidResult();
        JSInterop.SetupVoid("medicalMap.dispose", _ => true).SetVoidResult();
    }

    [Fact]
    public void Map_WithoutLocationPermission_SearchesAroundWarsawAndShowsTheEmptyArea()
    {
        JSInterop.Setup<Doctors.Centre>("medicalMap.locate").SetException(new JSException("denied"));
        var page = Render<Doctors>();

        page.FindAll("button").Single(button => button.TextContent == "Mapa").Click();

        page.WaitForAssertion(() => Assert.Contains("pokazujemy okolice Warszawy", page.Markup));
        Assert.Contains("Latitude=52.23&Longitude=21.01&RadiusKm=30", Assert.Single(searches));
        Assert.Contains("Brak lekarzy w promieniu 30 km od: Warszawa (domyślnie)", page.Markup);
        Assert.DoesNotContain("d-none", page.Find(".doctor-map").ClassName);
        page.WaitForAssertion(() => Assert.Single(JSInterop.Invocations["medicalMap.showDoctors"]));
    }

    [Fact]
    public void MyLocation_WhenShared_SearchesAroundTheRoundedPosition()
    {
        JSInterop.Setup<Doctors.Centre>("medicalMap.locate").SetResult(new Doctors.Centre(50.0614, 19.9366));
        var page = Render<Doctors>();

        page.FindAll("button").Single(button => button.TextContent == "Użyj mojej lokalizacji").Click();

        page.WaitForAssertion(() => Assert.Contains("Latitude=50.06&Longitude=19.94&RadiusKm=30", Assert.Single(searches)));
        Assert.Contains("Szukam w promieniu 30 km od: Twoja lokalizacja", page.Markup);
        Assert.DoesNotContain("Warszawy", page.Markup);
        Assert.Contains("d-none", page.Find(".doctor-map").ClassName);
    }

    private static Task<HttpResponseMessage> Ok(object body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}

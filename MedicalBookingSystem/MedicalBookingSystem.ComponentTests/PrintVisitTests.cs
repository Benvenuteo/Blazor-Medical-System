using System.Net;
using System.Net.Http.Json;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class PrintVisitTests : BunitContext
{
    [Fact]
    public void ShowsTheVisitDocumentation_AndPrintsThroughTheBrowser()
    {
        Services.AddSingleton(new HttpClient(new Handler(HttpStatusCode.OK)) { BaseAddress = new Uri("http://localhost") });
        JSInterop.SetupVoid("print");
        var page = Render<PrintVisit>(p => p.Add(c => c.Id, 7));

        page.WaitForAssertion(() => Assert.Contains("Notatka testowa", page.Markup));
        Assert.Contains("Lek testowy", page.Markup);
        Assert.Contains("Anna Testowa", page.Markup);
        Assert.Contains("15.11.2026", page.Markup);
        page.FindAll("button").Single(b => b.TextContent.Contains("Drukuj")).Click();
        JSInterop.VerifyInvoke("print");
    }

    [Fact]
    public void ForeignOrMissingVisit_ShowsTheApiMessage_WithoutAPrintButton()
    {
        Services.AddSingleton(new HttpClient(new Handler(HttpStatusCode.NotFound)) { BaseAddress = new Uri("http://localhost") });
        var page = Render<PrintVisit>(p => p.Add(c => c.Id, 99));

        page.WaitForAssertion(() => Assert.Contains("Nie znaleziono wizyty", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Drukuj"));
    }

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(status == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = 7, date = new DateTime(2026, 11, 15, 10, 0, 0), doctorName = "Anna Testowa", status = "Completed",
                        doctor = new { id = 1, firstName = "Anna", lastName = "Testowa" },
                        patient = new { id = 2, firstName = "Jan", lastName = "Pacjent" },
                        note = new { documentType = "note", id = 1, content = "Notatka testowa" },
                        prescription = new { documentType = "prescription", id = 2, medication = "Lek testowy", dosage = "1x dziennie", instructions = "Po jedzeniu", expiryDate = new DateTime(2026, 12, 15) }
                    })
                }
                : new HttpResponseMessage(status) { Content = JsonContent.Create(new { detail = "Nie znaleziono wizyty." }) });
    }
}

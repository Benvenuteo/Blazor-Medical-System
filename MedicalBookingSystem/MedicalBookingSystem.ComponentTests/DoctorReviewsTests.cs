using System.Net;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Pages;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class DoctorReviewsTests : BunitContext
{
    [Theory]
    [InlineData(true, "Nie udało się pobrać opinii")]
    [InlineData(false, "Brak opinii dla tego lekarza")]
    public void Reviews_WhenRequestFailsOrReturnsEmptyList_RendersControlledMessage(bool failRequest, string expectedMessage)
    {
        Services.AddSingleton(new HttpClient(new DoctorApiHandler(failRequest)) { BaseAddress = new Uri("http://localhost") });
        Services.AddSingleton(new Mock<ILocalStorageService>().Object);
        var component = Render<Doctors>();

        component.Find("form").Submit();
        component.WaitForAssertion(() => Assert.Contains("Pokaż opinie", component.Markup));
        component.FindAll("button").Single(button => button.TextContent.Contains("Pokaż opinie")).Click();

        component.WaitForAssertion(() => Assert.Contains(expectedMessage, component.Markup));
    }

    [Fact]
    public void RejectedReview_FromHistory_KeepsFormAndDraftVisible()
    {
        Services.AddSingleton(new HttpClient(new DoctorApiHandler(false)) { BaseAddress = new Uri("http://localhost") });
        var component = Render<AppointmentsHistory>();
        component.WaitForAssertion(() => Assert.Contains("Oceń lekarza", component.Markup));
        component.FindAll("button").Single(b => b.TextContent == "Oceń lekarza").Click();
        component.Find("#rating").Change("5");
        component.Find("#comment").Change("Treść do poprawienia");
        component.FindAll("form").Last().Submit();
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Błąd dodawania opinii", component.Find("[role=alert]").TextContent);
            Assert.Contains("Treść do poprawienia", component.FindComponent<Microsoft.AspNetCore.Components.Forms.InputTextArea>().Instance.Value);
            Assert.Contains("Anna Testowa", component.Find("table").TextContent);
        });
    }

    [Fact]
    public void AcceptedReview_FromHistory_ShowsConfirmationAndClosesForm()
    {
        Services.AddSingleton(new HttpClient(new DoctorApiHandler(false, acceptReview: true)) { BaseAddress = new Uri("http://localhost") });
        var component = Render<AppointmentsHistory>();
        component.WaitForAssertion(() => Assert.Contains("Oceń lekarza", component.Markup));
        component.FindAll("button").Single(b => b.TextContent == "Oceń lekarza").Click();
        component.Find("#rating").Change("4");
        component.FindAll("form").Last().Submit();
        component.WaitForAssertion(() =>
        {
            Assert.Contains("opinia o lekarzu Anna Testowa została dodana", component.Find("[role=status]").TextContent);
            Assert.Empty(component.FindAll("#rating"));
        });
    }

    [Theory]
    [InlineData(0, 0.0, "Brak ocen")]
    [InlineData(3, 4.6, "4,6")]
    [InlineData(2, 2.5, "2,5")]
    public void DoctorRating_ShowsValueWithDecimalComma(int reviewCount, double average, string expected)
    {
        Services.AddSingleton(new HttpClient(new DoctorApiHandler(false, reviewCount: reviewCount, averageRating: average)) { BaseAddress = new Uri("http://localhost") });
        var component = Render<Doctors>();

        component.Find("form").Submit();

        component.WaitForAssertion(() => Assert.Contains(expected, component.Find("tbody tr").TextContent));
        Assert.DoesNotContain("Dodaj opinię", component.Markup);
    }

    private sealed class DoctorApiHandler(bool failReviews, bool acceptReview = false, int reviewCount = 0, double averageRating = 0) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var response = path switch
            {
                "/api/doctors/specializations" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) },
                "/api/doctors" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { items = new[] { new { id = 1, firstName = "Anna", lastName = "Testowa", averageRating, reviewCount } }, totalCount = 1, page = 1, pageSize = 10 })
                },
                "/api/appointments/history" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { items = new[] { new { id = 5, date = new DateTime(2026, 10, 20, 10, 0, 0), doctorId = 1, doctorName = "Anna Testowa", status = "Completed" } }, totalCount = 1, page = 1, pageSize = 10 })
                },
                "/api/reviews" when acceptReview => new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = 1, doctorId = 1, rating = 4 }) },
                "/api/reviews" => new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "Możesz dodać tylko jedną opinię na lekarza." }) },
                "/api/reviews/doctor/1" when failReviews => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                "/api/reviews/doctor/1" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MedicalBookingSystem.SharedKernel.PagedResult<object>([], 0, 1, 10)) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
            return Task.FromResult(response);
        }
    }
}

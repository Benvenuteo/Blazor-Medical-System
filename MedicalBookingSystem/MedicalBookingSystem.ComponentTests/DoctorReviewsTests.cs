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
    public void RejectedReview_KeepsFormAndDraftVisible()
    {
        Services.AddSingleton(new HttpClient(new DoctorApiHandler(false)) { BaseAddress = new Uri("http://localhost") });
        var component = Render<Doctors>();
        component.Find("form").Submit();
        component.WaitForAssertion(() => Assert.Contains("Dodaj opinię", component.Markup));
        component.FindAll("button").Single(b => b.TextContent == "Dodaj opinię").Click();
        component.Find("#rating").Change("5");
        component.Find("#comment").Change("Treść do poprawienia");
        component.FindAll("form").Last().Submit();
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Błąd dodawania opinii", component.Find("[role=alert]").TextContent);
            Assert.Contains("Treść do poprawienia", component.FindComponent<Microsoft.AspNetCore.Components.Forms.InputTextArea>().Instance.Value);
            Assert.Contains("Anna", component.Find("table").TextContent);
        });
    }

    private sealed class DoctorApiHandler(bool failReviews) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var response = path switch
            {
                "/api/doctors/specializations" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) },
                "/api/doctors" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { items = new[] { new { id = 1, firstName = "Anna", lastName = "Testowa", averageRating = 0, reviewCount = 0 } }, totalCount = 1, page = 1, pageSize = 10 })
                },
                "/api/reviews/doctor/1" when failReviews => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                "/api/reviews/doctor/1" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MedicalBookingSystem.SharedKernel.PagedResult<object>([], 0, 1, 10)) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
            return Task.FromResult(response);
        }
    }
}

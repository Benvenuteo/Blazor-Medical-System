using System.Net;
using System.Net.Http.Json;
using Bunit;
using MedicalBookingSystem.BlazorWasm.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MedicalBookingSystem.ComponentTests;

public sealed class RequestOrderingTests : BunitContext
{
    [Fact]
    public async Task Search_IgnoresOlderResponse()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>();
        var second = new TaskCompletionSource<HttpResponseMessage>();
        var calls = 0;
        Services.AddSingleton(new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("specializations")) return Task.FromResult(Ok(Array.Empty<object>()));
            return ++calls == 1 ? first.Task : second.Task;
        })) { BaseAddress = new Uri("http://localhost") });
        var page = Render<Doctors>();
        var older = page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Equal(1, calls));
        var newer = page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Equal(2, calls));
        second.SetResult(Doctors("Nowszy"));
        await newer;
        first.SetResult(Doctors("Starszy"));
        await older;
        Assert.Contains("Nowszy", page.Find("table").TextContent);
        Assert.DoesNotContain("Starszy", page.Markup);
    }

    [Fact]
    public async Task HistoryDetails_IgnoresOlderResponse()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>();
        var second = new TaskCompletionSource<HttpResponseMessage>();
        Services.AddSingleton(new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/appointments/history" => Task.FromResult(Ok(new { items = new[] { new { id = 1 }, new { id = 2 } }, totalCount = 2, page = 1, pageSize = 10 })),
            "/api/appointments/1" => first.Task,
            _ => second.Task
        })) { BaseAddress = new Uri("http://localhost") });
        var page = Render<AppointmentsHistory>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody button").Count));
        var older = page.FindAll("tbody button")[0].ClickAsync(new MouseEventArgs());
        var newer = page.FindAll("tbody button")[1].ClickAsync(new MouseEventArgs());
        second.SetResult(Ok(new { id = 2, note = new { content = "Najnowsze szczegóły" } }));
        await newer;
        first.SetResult(Ok(new { id = 1, note = new { content = "Nieaktualne szczegóły" } }));
        await older;
        Assert.Contains("Najnowsze szczegóły", page.Markup);
        Assert.DoesNotContain("Nieaktualne szczegóły", page.Markup);
    }

    private static HttpResponseMessage Doctors(string name) => Ok(new { items = new[] { new { id = 1, firstName = name, lastName = "Testowy" } }, totalCount = 1, page = 1, pageSize = 10 });
    private static HttpResponseMessage Ok(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}

using Blazored.LocalStorage;
using MedicalBookingSystem.BlazorWasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (!Uri.TryCreate(builder.Configuration["MedicalBookingSystemAPIUrl"], UriKind.Absolute, out var apiAddress) ||
    apiAddress.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("Configure MedicalBookingSystemAPIUrl with an absolute HTTP or HTTPS URL.");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiAddress });
builder.Services.AddMudServices();
await builder.Build().RunAsync();

using MedicalBookingSystem.BlazorWasm.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<PatientAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<PatientAuthenticationStateProvider>());
builder.Services.AddScoped(services => new HttpClient(new ApiAuthenticationHandler(
    services.GetRequiredService<PatientAuthenticationStateProvider>(), services.GetRequiredService<NavigationManager>(), apiAddress)
    { InnerHandler = new HttpClientHandler() }) { BaseAddress = apiAddress });
builder.Services.AddMudServices();
await builder.Build().RunAsync();

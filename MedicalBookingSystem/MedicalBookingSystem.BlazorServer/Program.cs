using MedicalBookingSystem.Application;
using MedicalBookingSystem.BlazorServer.Service;
using MedicalBookingSystem.Infrastructure;
using MudBlazor.Services;
using NLog.Web;
using Radzen;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddApplicationServices(builder.Configuration["Clinic:TimeZone"] ?? "Europe/Warsaw");
var connectionString = MedicalBookingSystem.Infrastructure.DependencyInjection.ResolveConnectionString(
    builder.Configuration.GetConnectionString("MedicalBookingSystem") ?? "Data Source=data/medical.db",
    builder.Environment.ContentRootPath);
builder.Services.AddInfrastructureServices(connectionString);
builder.Services.AddMudServices();
builder.Services.AddRadzenComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DoctorImageUploader>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();

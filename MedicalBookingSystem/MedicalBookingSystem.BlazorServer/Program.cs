using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.BlazorServer.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddScoped<ICurrentUser, CircuitCurrentUser>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.Cookie.Name = "MedicalStaff";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = false;
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().RequireRole(Roles.Doctor, Roles.Admin).Build());
builder.Services.AddApplicationServices(builder.Configuration["Clinic:TimeZone"] ?? "Europe/Warsaw");
var connectionString = MedicalBookingSystem.Infrastructure.DependencyInjection.ResolveConnectionString(
    builder.Configuration.GetConnectionString("MedicalBookingSystem") ?? "Data Source=data/medical.db",
    builder.Environment.ContentRootPath);
builder.Services.AddInfrastructureServices(connectionString);
builder.Services.AddMudServices();
builder.Services.AddRadzenComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DoctorImageUploader>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});

var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapBlazorHub(options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapFallbackToPage("/_Host");
app.Run();

namespace MedicalBookingSystem.BlazorServer { public sealed class StaffEntryPoint; }

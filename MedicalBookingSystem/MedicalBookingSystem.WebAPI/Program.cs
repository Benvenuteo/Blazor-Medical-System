using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedicalBookingSystem.Application;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.WebAPI.JWT;
using MedicalBookingSystem.WebAPI.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddApplicationServices(builder.Configuration["Clinic:TimeZone"] ?? "Europe/Warsaw");
var connectionString = MedicalBookingSystem.Infrastructure.DependencyInjection.ResolveConnectionString(
    builder.Configuration.GetConnectionString("MedicalBookingSystem") ?? "Data Source=data/medical.db",
    builder.Environment.ContentRootPath);
builder.Services.AddInfrastructureServices(connectionString);
builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection("Seed"));

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new MedicalDocumentConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<ExceptionMiddleware>();

// Local demo instances get a process-local key; deployed hosts must configure a real secret.
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["JwtSettings:SecretKey"]))
    builder.Configuration["JwtSettings:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("JwtSettings"))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JWT issuer is required.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JWT audience is required.")
    .Validate(settings => Encoding.UTF8.GetByteCount(settings.SecretKey ?? string.Empty) >= 32,
        "Configure JwtSettings__SecretKey with at least 32 bytes.")
    .ValidateOnStart();
builder.Services.AddScoped<JwtService>();
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
{
    var settings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = settings.Issuer,
        ValidAudience = settings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("MedicalBookingSystem", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();
app.UseCors("MedicalBookingSystem");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

if (builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

app.Run();

// Exposes the actual application entry point to WebApplicationFactory integration tests.
public partial class Program;

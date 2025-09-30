using FluentValidation;
using FluentValidation.AspNetCore;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.Infrastructure.Repositories;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using MedicalBookingSystem.WebAPI.JWT;
using MedicalBookingSystem.WebAPI.Middleware;
using Microsoft.AspNet.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System.Text;
using System.Text.Json;

// Early init of NLog to allow startup and exception logging, before host is built
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.

    // NLog: Setup NLog for Dependency injection
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // rejestracja automappera w kontenerze IoC
    builder.Services.AddAutoMapper(typeof(MedicalBookingSystemMappingProfile));

    // rejestracja automatycznej walidacji (FluentValidation waliduje i przekazuje wynik przez ModelState)
    builder.Services.AddFluentValidationAutoValidation();

    // rejestracja kontekstu bazy w kontenerze IoC
    // var sqliteConnectionString = "Data Source=Kiosk.WebAPI.Logger.db";
    var sqliteConnectionString = @"Data Source=c:\Projekt_Aplikacje_Biznesowe\MedicalBookingSystem\MedicalSystem.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));

    // rejestracja walidatora
    builder.Services.AddScoped<IValidator<RegisterPatientDto>, RegisterPatientDtoValidator>();
    builder.Services.AddScoped<IValidator<LoginDto>, LoginDtoValidator>();
    builder.Services.AddScoped<IValidator<CreateAppointmentDto>, CreateAppointmentDtoValidator>();
    builder.Services.AddScoped<IValidator<UpdateAppointmentDto>, UpdateAppointmentDtoValidator>();
    builder.Services.AddScoped<IValidator<CreateReviewDto>, CreateReviewDtoValidator>();
    builder.Services.AddScoped<IValidator<CreatePrescriptionDto>, CreatePrescriptionDtoValidator>();
    builder.Services.AddScoped<IValidator<CreateNoteDto>, CreateNoteDtoValidator>();
    builder.Services.AddScoped<IValidator<SearchDoctorsDto>, SearchDoctorsDtoValidator>();
    builder.Services.AddScoped<IValidator<UpdatePatientDto>, UpdatePatientDtoValidator>();
    builder.Services.AddScoped<IValidator<CreateScheduleDto>, CreateScheduleDtoValidator>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<DataSeeder>();
    builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

    builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
    builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
    builder.Services.AddScoped<IDoctorScheduleRepository, DoctorScheduleRepository>();
    builder.Services.AddScoped<INoteRepository, NoteRepository>();
    builder.Services.AddScoped<IPatientRepository, PatientRepository>();
    builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
    builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
    builder.Services.AddScoped<ISpecializationRepository, SpecializationRepository>();

    builder.Services.AddScoped<IAppointmentService, AppointmentService>();
    builder.Services.AddScoped<IDoctorService, DoctorService>();
    builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
    builder.Services.AddScoped<IPatientService, PatientService>();
    builder.Services.AddScoped<IReviewService, ReviewService>();

    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new MedicalDocumentConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddScoped<ExceptionMiddleware>();

    builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
    });

    builder.Services.AddAuthorization();

    // rejestruje w kontenerze zale¿noœci politykê CORS o nazwie MedicalBookingSystem,
    // która zapewnia dostêp do API z dowolnego miejsca oraz przy pomocy dowolnej metody
    builder.Services.AddCors(o => o.AddPolicy("MedicalBookingSystem", builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    }));


    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseStaticFiles();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ExceptionMiddleware>();

    app.UseHttpsRedirection();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    // wstawia politykê CORS obs³ugi do potoku ¿¹dania
    app.UseCors("MedicalBookingSystem");

    // seeding data
    using (var scope = app.Services.CreateScope())
    {
        var dataSeeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        dataSeeder.Seed();
    }

    app.Run();
}
catch (Exception exception)
{
    // NLog: catch setup errors
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    NLog.LogManager.Shutdown();
}







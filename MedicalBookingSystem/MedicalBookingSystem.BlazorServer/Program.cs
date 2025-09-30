using FluentValidation;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.BlazorServer.Service;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.Infrastructure.Repositories;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using Microsoft.AspNet.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using NLog;
using NLog.Web;
using Radzen;


// Early init of NLog to allow startup and exception logging, before host is built
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");
try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    // rejestracja automappera w kontenerze IoC
    builder.Services.AddAutoMapper(typeof(MedicalBookingSystemMappingProfile));

    // rejestracja kontekstu bazy w kontenerze IoC
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
    builder.Services.AddScoped<DataSeeder>();

    builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
    builder.Services.AddMudServices();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
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
    builder.Services.AddScoped<DoctorImageUploader>();
    builder.Services.AddRadzenComponents();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseStaticFiles();

    app.UseRouting();

    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    // seeding data
    //using (var scope = app.Services.CreateScope())
    //{
    //    var dataSeeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    //    dataSeeder.Seed();
    //}

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
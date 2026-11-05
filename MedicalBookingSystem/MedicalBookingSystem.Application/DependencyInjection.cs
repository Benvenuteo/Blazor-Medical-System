using FluentValidation;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedicalBookingSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, string timeZoneId)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new ClinicClock(
            provider.GetRequiredService<TimeProvider>(), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)));
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
        services.AddScoped<IPasswordHasher<Patient>, PasswordHasher<Patient>>();
        services.AddScoped<IPasswordHasher<StaffAccount>, PasswordHasher<StaffAccount>>();
        services.AddScoped<StaffAuthenticationService>();
        services.AddScoped<PatientAuthenticationService>();
        services.AddScoped<AccountRecoveryService>();
        services.AddScoped<ExternalAuthenticationService>();
        services.TryAddSingleton<IExternalTokenValidator, DisabledExternalTokenValidator>();
        services.AddOptions<Notifications.NotificationOptions>();
        services.AddScoped<ISessionValidator, AccountSessionValidator>();
        services.AddScoped<SessionGuard>();
        services.AddScoped<StaffPasswordService>();
        services.AddScoped<SessionManagementService>();
        services.AddScoped<StaffAccountService>();
        services.AddScoped<WorklistService>();
        services.AddScoped<UserAccess>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IMedicalRecordService, MedicalRecordService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ClinicService>();
        return services;
    }
}

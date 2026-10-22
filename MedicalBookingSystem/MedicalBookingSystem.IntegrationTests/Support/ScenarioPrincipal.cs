using System.Security.Claims;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Support;

internal sealed class ScenarioPrincipal(ClaimsPrincipal principal) : ICurrentUser
{
    public Task<ClaimsPrincipal> GetPrincipalAsync() => Task.FromResult(principal);
    public static async Task<ClaimsPrincipal> PatientAsync(IServiceProvider services, int patientId)
    {
        using var context = await services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var patient = await context.Patients.SingleAsync(p => p.Id == patientId);
        return AccountClaims.Create(patient.Id, patient.FirstName, patient.Email, Roles.Patient,
            new DateTimeOffset(services.GetRequiredService<ClinicClock>().UtcNow).AddHours(1), securityStamp: patient.SecurityStamp);
    }
    public static async Task<ClaimsPrincipal> StaffAsync(IServiceProvider services, string email)
    {
        using var context = await services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var staff = await context.StaffAccounts.SingleAsync(p => p.Email == email);
        return AccountClaims.Create(staff.Id, staff.DisplayName, staff.Email, staff.Role.ToString(),
            new DateTimeOffset(services.GetRequiredService<ClinicClock>().UtcNow).AddHours(1), staff.DoctorId, staff.SecurityStamp, staff.MustChangePassword);
    }
    public static UserAccess Access(IServiceProvider services, ClaimsPrincipal principal) => new(new ScenarioPrincipal(principal), services.GetRequiredService<SessionGuard>());
}

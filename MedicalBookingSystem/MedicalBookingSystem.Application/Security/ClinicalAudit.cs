using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Security;

public static class ClinicalAudit
{
    /// <summary>Append to the caller's unit so clinical writes and their audit commit atomically.</summary>
    public static async Task AppendAsync(IUnitOfWork unit, UserAccess access, ClinicClock clock,
        string action, string resource, int resourceId, int patientId)
    {
        var identity = await access.IdentityAsync();
        await unit.ClinicalAudit.AddAsync(new ClinicalAuditEntry
        {
            OccurredAtUtc = clock.UtcNow,
            Actor = $"{(identity.Role == Roles.Patient ? "patient" : "staff")}:{identity.Id}",
            ActorRole = identity.Role, Action = action, Resource = resource,
            ResourceId = resourceId, PatientId = patientId
        });
    }
}

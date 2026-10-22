using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
namespace MedicalBookingSystem.Infrastructure.Repositories;

public sealed class ClinicalAuditWriter(ApplicationDbContext db) : IClinicalAuditWriter
{
    public async Task AddAsync(ClinicalAuditEntry entry) => await db.ClinicalAudit.AddAsync(entry);
}

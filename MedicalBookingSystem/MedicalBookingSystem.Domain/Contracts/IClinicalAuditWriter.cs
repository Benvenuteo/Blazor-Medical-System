using MedicalBookingSystem.Domain.Models;
namespace MedicalBookingSystem.Domain.Contracts;

public interface IClinicalAuditWriter
{
    Task AddAsync(ClinicalAuditEntry entry);
}

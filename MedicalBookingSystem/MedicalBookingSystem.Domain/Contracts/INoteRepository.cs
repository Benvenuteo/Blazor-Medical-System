using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface INoteRepository : IRepository<Note>
    {
        Task<IEnumerable<Note>> GetByPatientIdAsync(int patientId);
    }
}

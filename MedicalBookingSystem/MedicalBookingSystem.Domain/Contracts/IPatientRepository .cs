using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IPatientRepository : IRepository<Patient>
    {
        Task<Patient> GetByEmailAsync(string email);
    }
}

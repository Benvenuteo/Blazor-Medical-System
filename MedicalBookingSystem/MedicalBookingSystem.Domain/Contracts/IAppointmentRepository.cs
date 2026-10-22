using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IAppointmentRepository : IRepository<Appointment>
    {
        Task<Appointment?> GetAppointmentByIdAsync(int id);
    }
}

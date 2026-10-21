using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IAppointmentRepository : IRepository<Appointment>
    {
        Task<IEnumerable<Appointment>> GetUpcomingByPatientIdAsync(int patientId);
        Task<IEnumerable<Appointment>> GetHistoryByPatientIdAsync(int patientId);
        Task<Appointment?> GetAppointmentByIdAsync(int id);
        Task<IEnumerable<Appointment>> GetScheduledByDoctorIdAsync(int doctorId);
        Task<IEnumerable<Appointment>> GetCompletedByDoctorIdAsync(int doctorId);
    }
}

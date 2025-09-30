using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IAppointmentRepository : IRepository<Appointment>
    {
        Task CancelAppointmentAsync(int id);
        Task<IEnumerable<Appointment>> GetUpcomingByPatientIdAsync(int patientId);
        Task<IEnumerable<Appointment>> GetHistoryByPatientIdAsync(int patientId);
        Task<Appointment> GetAppointmentByIdAsync(int id);
        List<Appointment> GetNextAppointmentsByDoctorId(int doctorId);
        List<Appointment> GetPreviousAppointmentsByDoctorId(int doctorId);
    }
}

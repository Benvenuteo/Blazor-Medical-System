using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IAppointmentService
    {
        Task<Result<AppointmentDto>> CreateAppointmentAsync(CreateAppointmentDto dto, int patientId);
        Task<Result> CancelAppointmentAsync(int id, int patientId);
        Task<Result> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto);
        Task<Result<IEnumerable<AppointmentDto>>> GetUpcomingAppointmentsAsync(int patientId);
        Task<Result<IEnumerable<AppointmentDto>>> GetPastAppointmentsAsync(int patientId);
        Task<Result<AppointmentDetailsDto>> GetAppointmentDetailsAsync(int appointmentId, int patientId);
        List<AppointmentDto> GetNextAppointmentsByDoctorId(int doctorId);
        List<AppointmentDto> GetPreviousAppointmentsByDoctorId(int doctorId);
    }
}

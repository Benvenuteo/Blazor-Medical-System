using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IDoctorScheduleRepository
    {
        Task<DoctorSchedule?> GetByIdAsync(int id);
        Task<IEnumerable<DoctorSchedule>> GetByDoctorIdAsync(int doctorId);
        Task<IEnumerable<DoctorSchedule>> GetInRangeAsync(int doctorId, DateTime from, DateTime to);
        Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime date);
        Task AddAsync(DoctorSchedule schedule);
        Task AddRangeAsync(IEnumerable<DoctorSchedule> schedules);
        void Update(DoctorSchedule schedule);
        void Remove(DoctorSchedule schedule);
        Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime date, int? excludedAppointmentId = null);
        /// <summary>Clinic of the shift that covers a visit starting at <paramref name="start"/>.</summary>
        Task<int?> FindClinicAtAsync(int doctorId, DateTime start);
        Task<bool> AnyInClinicAsync(int clinicId, int? doctorId = null);
    }
}

using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IDoctorScheduleRepository
    {
        Task<DoctorSchedule> GetByIdAsync(int id);
        Task<IEnumerable<DoctorSchedule>> GetByDoctorIdAsync(int doctorId);
        Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime date);
        Task AddAsync(DoctorSchedule schedule);
        Task AddRangeAsync(IEnumerable<DoctorSchedule> schedules);
        void Update(DoctorSchedule schedule);
        void Remove(DoctorSchedule schedule);
        Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime date);
    }
}

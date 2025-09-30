using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class DoctorScheduleRepository : IDoctorScheduleRepository
    {
        private readonly ApplicationDbContext _context;

        public DoctorScheduleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorSchedule> GetByIdAsync(int id)
        {
            return await _context.DoctorSchedules.FindAsync(id);
        }

        public async Task<IEnumerable<DoctorSchedule>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.DoctorSchedules
                .Where(s => s.DoctorId == doctorId)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime date)
        {
            return await _context.DoctorSchedules
                .AnyAsync(s => s.DoctorId == doctorId &&
                              s.StartTime <= date &&
                              s.EndTime >= date &&
                              s.IsAvailable);
        }

        public async Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime date)
        {
            //  Czy lekarz ma w grafiku ten termin
            var isInSchedule = await _context.DoctorSchedules
                .AnyAsync(s => s.DoctorId == doctorId &&
                              s.StartTime <= date &&
                              s.EndTime >= date &&
                              s.IsAvailable);

            if (!isInSchedule)
                return false;

            // Sprawdź czy termin nie jest już zajęty
            return !await _context.Appointments
                .AnyAsync(a => a.DoctorId == doctorId &&
                              a.Date == date &&
                              a.Status != AppointmentStatus.Cancelled);
        }

        public async Task AddAsync(DoctorSchedule schedule)
        {
            await _context.DoctorSchedules.AddAsync(schedule);
        }

        public async Task AddRangeAsync(IEnumerable<DoctorSchedule> schedules)
        {
            await _context.DoctorSchedules.AddRangeAsync(schedules);
        }

        public void Update(DoctorSchedule schedule)
        {
            _context.DoctorSchedules.Update(schedule);
        }

        public void Remove(DoctorSchedule schedule)
        {
            _context.DoctorSchedules.Remove(schedule);
        }
    }
}

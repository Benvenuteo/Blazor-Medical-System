using MedicalBookingSystem.Domain.Scheduling;
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

        public async Task<DoctorSchedule?> GetByIdAsync(int id)
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

        public async Task<IEnumerable<DoctorSchedule>> GetInRangeAsync(int doctorId, DateTime from, DateTime to) =>
            await _context.DoctorSchedules.AsNoTracking().Where(s => s.DoctorId == doctorId && s.EndTime > from && s.StartTime <= to)
                .OrderBy(s => s.StartTime).ToListAsync();

        public async Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime date)
        {
            return await _context.DoctorSchedules
                .AnyAsync(s => s.DoctorId == doctorId &&
                              s.StartTime <= date &&
                              s.EndTime >= date.AddMinutes(AppointmentSlots.DurationMinutes) &&
                              s.IsAvailable);
        }

        public async Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime date, int? excludedAppointmentId = null)
        {
            var end = date.Add(AppointmentSlots.Duration);
            var previousStart = date.Subtract(AppointmentSlots.Duration);
            if (!AppointmentSlots.IsAligned(date)) return false;
            var isInSchedule = await _context.DoctorSchedules.AnyAsync(s => s.DoctorId == doctorId &&
                s.StartTime <= date && s.EndTime >= end && s.IsAvailable);
            return isInSchedule && !await _context.Appointments.AnyAsync(a => a.DoctorId == doctorId &&
                a.Id != excludedAppointmentId && a.Date < end && a.Date > previousStart && a.Status != AppointmentStatus.Cancelled);
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

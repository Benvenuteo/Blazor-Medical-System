using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
    {
        private readonly ClinicClock _clock;
        public AppointmentRepository(ApplicationDbContext context, ClinicClock clock) : base(context) => _clock = clock;

        public async Task<IEnumerable<Appointment>> GetUpcomingByPatientIdAsync(int patientId)
        {
            return await _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patientId &&
                           a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetHistoryByPatientIdAsync(int patientId)
        {
            return await _dbSet
                .Include(a => a.Doctor)
                .Include(a => a.Note)
                .Include(a => a.Prescription)
                .Where(a => a.PatientId == patientId &&
                           a.Date <= _clock.Now &&
                           a.Status == AppointmentStatus.Completed)
                .OrderByDescending(a => a.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Appointment?> GetAppointmentByIdAsync(int id)
        {
            return await _dbSet
                    .Where(a => a.Id == id)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.DoctorSpecializations)
                            .ThenInclude(ds => ds.Specialization)
                    .Include(a => a.Note)
                    .Include(a => a.Prescription)
                    .Include(a => a.Patient)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Appointment>> GetScheduledByDoctorIdAsync(int doctorId)
        {
            return await _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.DoctorId == doctorId)
                .Include(a => a.Note)
                .Include(a => a.Prescription)
                .Include(a => a.Patient)
                .Where(a => a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetCompletedByDoctorIdAsync(int doctorId)
        {
            return await _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.DoctorId == doctorId)
                .Include(a => a.Note)
                .Include(a => a.Prescription)
                .Include(a => a.Patient)
                .Where(a => a.Date < _clock.Now &&
                           a.Status == AppointmentStatus.Completed)
                .OrderByDescending(a => a.Date)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

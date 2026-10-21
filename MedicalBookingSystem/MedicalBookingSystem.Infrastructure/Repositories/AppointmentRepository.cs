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

        public async Task CancelAppointmentAsync(int id)
        {
            var appointment = await _dbSet.FindAsync(id);
            if (appointment == null) throw new KeyNotFoundException("Appointment not found");

            if (appointment.Date < _clock.Now.AddHours(24))
                throw new InvalidOperationException("Cannot cancel within 24 hours");

            appointment.Status = AppointmentStatus.Cancelled;
            _dbSet.Update(appointment);
        }

        public async Task<IEnumerable<Appointment>> GetUpcomingByPatientIdAsync(int patientId)
        {
            return await _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patientId &&
                           a.Date > _clock.Now &&
                           a.Status == AppointmentStatus.Scheduled)
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

        public List<Appointment> GetNextAppointmentsByDoctorId(int doctorId)
        {
            return _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.DoctorId == doctorId)
                .Include(a => a.Note)
                .Include(a => a.Prescription)
                .Include(a => a.Patient)
                .Where(a => a.Date > _clock.Now &&
                           a.Status == AppointmentStatus.Scheduled)
                .OrderByDescending(a => a.Date)
                .AsNoTracking()
                .ToList();
        }

        public List<Appointment> GetPreviousAppointmentsByDoctorId(int doctorId)
        {
            return _dbSet
                .Include(a => a.Doctor)
                .Where(a => a.DoctorId == doctorId)
                .Include(a => a.Note)
                .Include(a => a.Prescription)
                .Include(a => a.Patient)
                .Where(a => a.Date < _clock.Now &&
                           a.Status == AppointmentStatus.Completed)
                .OrderByDescending(a => a.Date)
                .AsNoTracking()
                .ToList();
        }
    }
}

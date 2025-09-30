using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class DoctorRepository : Repository<Doctor>, IDoctorRepository
    {
        public DoctorRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Doctor>> GetBySpecializationAsync(int specializationId)
        {
            return await _dbSet
                .Include(d => d.DoctorSpecializations)
                .ThenInclude(ds => ds.Specialization)
                .Where(d => d.DoctorSpecializations.Any(ds => ds.SpecializationId == specializationId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Doctor>> GetByRegionAsync(string region)
        {
            return await _dbSet
                .Where(d => d.Region == region)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Doctor>> GetAvailableDoctorsAsync(DateTime date)
        {
            return await _dbSet
                .Where(d => !d.Appointments.Any(a =>
                    a.Date.Date == date.Date &&
                    a.Status != AppointmentStatus.Cancelled))
                .Include(d => d.Schedules)
                .Where(d => d.Schedules.Any(s =>
                    s.StartTime.TimeOfDay <= date.TimeOfDay &&
                    s.EndTime.TimeOfDay >= date.TimeOfDay &&
                    s.IsAvailable))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<double> GetAverageRatingAsync(int doctorId)
        {
            var doctor = await _dbSet
                .Include(d => d.Reviews)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null || doctor.Reviews == null || !doctor.Reviews.Any())
                return 0.0;

            return doctor.Reviews.Average(r => r.Rating);
        }

        public async Task<IEnumerable<Doctor>> GetTopRatedDoctorsAsync(int count)
        {
            return await _dbSet
                .Select(d => new
                {
                    Doctor = d,
                    AverageRating = d.Reviews.Average(r => (double?)r.Rating) ?? 0.0
                })
                .OrderByDescending(x => x.AverageRating)
                .Take(count)
                .Select(x => x.Doctor)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Doctor?> GetByIdWithReviewsAsync(int doctorId)
        {
            return await _dbSet
                .Include(d => d.Reviews)
                .FirstOrDefaultAsync(d => d.Id == doctorId);
        }

        public async Task<IEnumerable<Doctor>> GetAllAsync()
        {
            var doctors = await _dbSet
                .Include(d => d.Appointments)
                .Include(d => d.Reviews)
                .Include(d => d.Schedules)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .ToListAsync();

            foreach (var doctor in doctors)
            {
                doctor.UpdateAverageRating();
            }

            return doctors;
        }
    }
}

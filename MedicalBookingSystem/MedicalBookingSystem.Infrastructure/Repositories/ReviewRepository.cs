using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class ReviewRepository : Repository<Review>, IReviewRepository
    {
        public ReviewRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Review>> GetByDoctorIdAsync(int doctorId)
        {
            return await _dbSet
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.CreatedDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<double> GetAverageRatingAsync(int doctorId)
        {
            return await _dbSet
                .Where(r => r.DoctorId == doctorId)
                .AverageAsync(r => (double?)r.Rating) ?? 0.0;
        }

        public async Task<IEnumerable<Review>> GetRecentReviewsAsync(int doctorId, int count)
        {
            return await _dbSet
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.CreatedDate)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> HasPatientReviewedDoctorAsync(int patientId, int doctorId)
        {
            return await _dbSet
                .AnyAsync(r => r.PatientId == patientId && r.DoctorId == doctorId);
        }
    }
}

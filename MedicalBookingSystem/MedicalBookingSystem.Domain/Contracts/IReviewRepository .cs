using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IReviewRepository : IRepository<Review>
    {
        Task<IEnumerable<Review>> GetByDoctorIdAsync(int doctorId);
        Task<double> GetAverageRatingAsync(int doctorId);
        Task<IEnumerable<Review>> GetRecentReviewsAsync(int doctorId, int count);
        Task<bool> HasPatientReviewedDoctorAsync(int patientId, int doctorId);
    }
}

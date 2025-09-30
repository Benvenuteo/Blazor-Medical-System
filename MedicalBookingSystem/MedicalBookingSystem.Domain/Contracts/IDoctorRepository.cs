using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IDoctorRepository : IRepository<Doctor>
    {
        Task<IEnumerable<Doctor>> GetBySpecializationAsync(int specializationId);
        Task<IEnumerable<Doctor>> GetByRegionAsync(string region);
        Task<IEnumerable<Doctor>> GetAvailableDoctorsAsync(DateTime date);
        Task<double> GetAverageRatingAsync(int doctorId);
        Task<IEnumerable<Doctor>> GetTopRatedDoctorsAsync(int count);
        Task<Doctor?> GetByIdWithReviewsAsync(int doctorId);
    }
}

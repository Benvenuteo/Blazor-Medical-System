using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IReviewService
    {
        Task<Result<ReviewDto>> AddReviewAsync(CreateReviewDto dto, int patientId);
        Task<Result<IEnumerable<ReviewDto>>> GetDoctorReviewsAsync(int doctorId);
        Task<Result> DeleteReviewAsync(int reviewId, int patientId);
        Task<Result<ReviewDto>> GetReviewAsync(int reviewId);
    }
}

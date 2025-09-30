using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IDoctorService
    {
        Task<Result<DoctorDetailsDto>> GetDoctorDetailsAsync(int id);
        Task<Result<IEnumerable<DoctorDto>>> SearchDoctorsAsync(SearchDoctorsDto dto);
        Task<Result<IEnumerable<DoctorScheduleDto>>> GetDoctorScheduleAsync(int doctorId);
        Task<Result<DoctorRatingDto>> GetDoctorRatingAsync(int doctorId);
        Task<Result<IEnumerable<DoctorDto>>> GetAllAsync();
        Task<Result<IEnumerable<SpecializationDto>>> GetSpecializationsAsync();
        Task<bool> SetDoctorImageUrlAsync(int doctorId, string imageUrl);
        Task<Result> CreateScheduleAsync(CreateScheduleDto dto);
    }
}

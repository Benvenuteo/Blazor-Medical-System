using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IDoctorService
    {
        Task<Result<DoctorDetailsDto>> GetDoctorDetailsAsync(int id);
        Task<Result<PagedResult<DoctorDto>>> SearchDoctorsAsync(SearchDoctorsDto dto);
        Task<Result<IEnumerable<DoctorScheduleDto>>> GetDoctorScheduleAsync(int doctorId);
        Task<Result<IEnumerable<SpecializationDto>>> GetSpecializationsAsync();
        Task<Result> CreateScheduleAsync(CreateScheduleDto dto);
        Task<Result<IReadOnlyList<AvailableSlotDto>>> GetAvailableSlotsAsync(int doctorId, DateTime? date = null, int? excludedAppointmentId = null);
    }
}

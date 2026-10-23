using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IPatientService
    {
        Task<Result<PatientDto>> GetPatientProfileAsync(int id);
        Task<Result> UpdatePatientAsync(int id, UpdatePatientDto dto);
    }
}

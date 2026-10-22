using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;

namespace MedicalBookingSystem.Application.Queries;

public interface IStaffAccountQueries
{
    Task<PagedResult<StaffAccountDto>> SearchAsync(AccountSearchDto search, int currentStaffId);
    Task<PagedResult<StaffDoctorOptionDto>> UnassignedDoctorsAsync(AccountSearchDto search);
}

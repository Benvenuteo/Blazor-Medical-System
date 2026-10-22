using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Queries;

public interface IWorklistQueries
{
    Task<PagedResult<AppointmentDto>> AppointmentsAsync(int ownerId, bool doctor, bool completed, AppointmentListQueryDto query);
    Task<PagedResult<MedicalDocumentSummaryDto>> DocumentsAsync(int patientId, PageQueryDto query);
    Task<PagedResult<ReviewDto>> ReviewsAsync(int doctorId, PageQueryDto query);
    Task<PagedResult<ClinicalAuditDto>> AuditAsync(PageQueryDto query);
}

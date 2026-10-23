using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;

namespace MedicalBookingSystem.Application.Queries;

/// <summary>Read model: database filtering, rating aggregation and paging without loading entity graphs.</summary>
public interface IDoctorDirectoryQueries
{
    Task<DoctorDetailsDto?> GetDetailsAsync(int doctorId, DateTime from, DateTime to);
    Task<PagedResult<DoctorDto>> SearchAsync(SearchDoctorsDto search, IReadOnlyList<DateTime> candidateStarts);
}

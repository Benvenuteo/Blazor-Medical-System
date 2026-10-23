using FluentValidation;
using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services;

/// <summary>Validate and authorize before executing any bounded read model query.</summary>
public sealed class WorklistService(IWorklistQueries queries, IUnitOfWorkFactory units, UserAccess access, ClinicClock clock,
    IValidator<PageQueryDto> pages, IValidator<AppointmentListQueryDto> appointments)
{
    public async Task<Result<PagedResult<AppointmentDto>>> PatientAppointmentsAsync(int patientId, bool completed, AppointmentListQueryDto query)
    {
        await access.RequirePatientAsync(patientId);
        var validation = await appointments.ValidateAsync(query);
        return validation.IsValid ? Result.Success(await queries.AppointmentsAsync(patientId, false, completed, query))
            : Result.Failure<PagedResult<AppointmentDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
    }
    public async Task<Result<PagedResult<AppointmentDto>>> DoctorAppointmentsAsync(int doctorId, bool completed, AppointmentListQueryDto query)
    {
        await access.RequireDoctorAsync(doctorId);
        var validation = await appointments.ValidateAsync(query);
        return validation.IsValid ? Result.Success(await queries.AppointmentsAsync(doctorId, true, completed, query))
            : Result.Failure<PagedResult<AppointmentDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
    }
    public async Task<Result<PagedResult<MedicalDocumentSummaryDto>>> DocumentsAsync(int patientId, PageQueryDto query)
    {
        await access.RequirePatientAsync(patientId);
        var validation = await pages.ValidateAsync(query);
        if (!validation.IsValid) return Result.Failure<PagedResult<MedicalDocumentSummaryDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        var result = await queries.DocumentsAsync(patientId, query);
        using var unit = units.Create();
        await ClinicalAudit.AppendAsync(unit, access, clock, "Read", "PatientDocuments", patientId, patientId);
        await unit.SaveChangesAsync();
        return Result.Success(result);
    }
    public async Task<Result<PagedResult<ReviewDto>>> ReviewsAsync(int doctorId, PageQueryDto query)
    {
        var validation = await pages.ValidateAsync(query);
        return validation.IsValid ? Result.Success(await queries.ReviewsAsync(doctorId, query))
            : Result.Failure<PagedResult<ReviewDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
    }
    public async Task<Result<PagedResult<ClinicalAuditDto>>> AuditAsync(PageQueryDto query)
    {
        await access.RequireAdminAsync();
        var validation = await pages.ValidateAsync(query);
        return validation.IsValid ? Result.Success(await queries.AuditAsync(query))
            : Result.Failure<PagedResult<ClinicalAuditDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
    }
}

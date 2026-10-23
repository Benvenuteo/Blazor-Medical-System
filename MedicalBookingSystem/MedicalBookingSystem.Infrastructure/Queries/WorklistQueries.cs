using System.Linq.Expressions;
using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Queries;

public sealed class WorklistQueries(IDbContextFactory<ApplicationDbContext> contexts, ClinicClock clock) : IWorklistQueries
{
    private static readonly Expression<Func<Appointment, AppointmentDto>> AppointmentProjection = a => new AppointmentDto
    {
        Id = a.Id, Date = a.Date, PatientId = a.PatientId, DoctorId = a.DoctorId,
        DoctorName = a.Doctor.FirstName + " " + a.Doctor.LastName, Status = a.Status.ToString(),
        HasNote = a.Note != null, HasPrescription = a.Prescription != null
    };

    public async Task<PagedResult<AppointmentDto>> AppointmentsAsync(int ownerId, bool doctor, bool completed, AppointmentListQueryDto query)
    {
        using var db = contexts.CreateDbContext();
        var rows = db.Appointments.AsNoTracking().Where(a => doctor ? a.DoctorId == ownerId : a.PatientId == ownerId);
        rows = rows.Where(a => a.Status == (completed ? AppointmentStatus.Completed : AppointmentStatus.Scheduled));
        if (completed) rows = rows.Where(a => a.Date <= clock.Now);
        if (query.From.HasValue) rows = rows.Where(a => a.Date >= query.From.Value);
        if (query.To.HasValue) rows = rows.Where(a => a.Date <= query.To.Value);
        if (query.MissingDocument == "Note") rows = rows.Where(a => a.Note == null);
        if (query.MissingDocument == "Prescription") rows = rows.Where(a => a.Prescription == null);
        var ordered = completed ? rows.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id) : rows.OrderBy(a => a.Date).ThenBy(a => a.Id);
        return await PageAsync(ordered.Select(AppointmentProjection), query);
    }

    public async Task<PagedResult<MedicalDocumentSummaryDto>> DocumentsAsync(int patientId, PageQueryDto query)
    {
        using var db = contexts.CreateDbContext();
        // Concat scalar projections before constructing DTOs, so UNION ALL and paging remain in SQL.
        var notes = db.Notes.Where(n => n.Appointment.PatientId == patientId)
            .Select(n => new { n.Id, n.AppointmentId, Type = "Note", n.CreatedDate });
        var prescriptions = db.Prescriptions.Where(p => p.Appointment.PatientId == patientId)
            .Select(p => new { p.Id, p.AppointmentId, Type = "Prescription", p.CreatedDate });
        var rows = notes.Concat(prescriptions).OrderByDescending(d => d.CreatedDate).ThenBy(d => d.Type).ThenByDescending(d => d.Id)
            .Select(d => new MedicalDocumentSummaryDto(d.Id, d.AppointmentId, d.Type, DateTime.SpecifyKind(d.CreatedDate, DateTimeKind.Utc)));
        return await PageAsync(rows, query);
    }
    public async Task<PagedResult<ReviewDto>> ReviewsAsync(int doctorId, PageQueryDto query)
    {
        using var db = contexts.CreateDbContext();
        return await PageAsync(db.Reviews.AsNoTracking().Where(r => r.DoctorId == doctorId).OrderByDescending(r => r.CreatedDate).ThenByDescending(r => r.Id)
            .Select(r => new ReviewDto { Id = r.Id, DoctorId = r.DoctorId, PatientId = r.PatientId, Rating = r.Rating, Comment = r.Comment }), query);
    }
    public async Task<PagedResult<ClinicalAuditDto>> AuditAsync(PageQueryDto query)
    {
        using var db = contexts.CreateDbContext();
        return await PageAsync(db.ClinicalAudit.AsNoTracking().OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)
            .Select(a => new ClinicalAuditDto(a.Id, DateTime.SpecifyKind(a.OccurredAtUtc, DateTimeKind.Utc), a.Actor, a.ActorRole, a.Action, a.Resource, a.ResourceId, a.PatientId)), query);
    }
    private static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> rows, PageQueryDto query)
    {
        var count = await rows.CountAsync();
        var items = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        return new(items, count, query.Page, query.PageSize);
    }
}

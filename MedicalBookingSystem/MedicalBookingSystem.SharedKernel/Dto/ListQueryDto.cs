namespace MedicalBookingSystem.SharedKernel.Dto;

public class PageQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class AppointmentListQueryDto : PageQueryDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? MissingDocument { get; set; }
}

public sealed record MedicalDocumentSummaryDto(int Id, int AppointmentId, string DocumentType, DateTime CreatedDate);
public sealed record ClinicalAuditDto(long Id, DateTime OccurredAtUtc, string Actor, string ActorRole,
    string Action, string Resource, int ResourceId, int PatientId);

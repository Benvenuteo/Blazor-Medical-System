namespace MedicalBookingSystem.Domain.Models;

/// <summary>Append-only access metadata. Never store clinical text, credentials, tokens or patient names here.</summary>
public sealed class ClinicalAuditEntry
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public int PatientId { get; set; }
}

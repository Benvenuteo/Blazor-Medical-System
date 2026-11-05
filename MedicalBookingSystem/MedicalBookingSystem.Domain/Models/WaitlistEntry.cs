namespace MedicalBookingSystem.Domain.Models;

/// <summary>
/// "Tell me when this doctor has a free slot before <see cref="Before"/>." One notification per entry:
/// after it is sent the entry is closed, and the patient books the slot like anyone else.
/// </summary>
public sealed class WaitlistEntry
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    /// <summary>Clinic-local time; only slots starting earlier are interesting.</summary>
    public DateTime Before { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? NotifiedAtUtc { get; set; }
    public bool Active { get; set; } = true;
}

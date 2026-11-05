namespace MedicalBookingSystem.Domain.Models;

/// <summary>An external identity (e.g. Google) linked to a patient. Subject is the stable user id of the provider, not the email.</summary>
public sealed class PatientExternalLogin
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime LinkedAtUtc { get; set; }
}

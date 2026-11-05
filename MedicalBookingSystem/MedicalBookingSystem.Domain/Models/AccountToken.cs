namespace MedicalBookingSystem.Domain.Models;

public enum AccountTokenPurpose { EmailConfirmation, PasswordReset }

/// <summary>One-time link token. Only its SHA-256 hash is stored, so a database leak does not expose working links.</summary>
public sealed class AccountToken
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public AccountTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

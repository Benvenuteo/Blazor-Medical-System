namespace MedicalBookingSystem.Domain.Models;

public enum StaffRole { Doctor, Admin }

public sealed class StaffAccount
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; } = Guid.NewGuid();
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
}

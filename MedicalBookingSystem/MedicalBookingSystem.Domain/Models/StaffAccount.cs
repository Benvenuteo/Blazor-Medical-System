namespace MedicalBookingSystem.Domain.Models;

public enum StaffRole { Doctor, Admin }

public sealed class StaffAccount
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
}

namespace MedicalBookingSystem.SharedKernel.Dto.Accounts;

public sealed class StaffAccountDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public int? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public Guid Version { get; set; }
    public bool IsCurrentUser { get; set; }
}

public sealed class AccountSearchDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
}

public sealed record StaffDoctorOptionDto(int Id, string DisplayName, string LicenseNumber);

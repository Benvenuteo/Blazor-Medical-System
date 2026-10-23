namespace MedicalBookingSystem.SharedKernel.Dto.Accounts;

public sealed class CreateStaffAccountDto
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Doctor";
    public int? DoctorId { get; set; }
    public string InitialPassword { get; set; } = "";
}

public sealed class UpdateStaffAccountDto
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public Guid Version { get; set; }
}

public sealed class ResetStaffPasswordDto
{
    public string NewPassword { get; set; } = "";
    public Guid Version { get; set; }
}

public sealed class ChangeStaffPasswordDto
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmNewPassword { get; set; } = "";
}

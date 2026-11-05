namespace MedicalBookingSystem.SharedKernel.Dto;

public sealed class ForgotPasswordDto
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public sealed class ConfirmEmailDto
{
    public string Token { get; set; } = string.Empty;
}

namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class UpdatePatientDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }
}

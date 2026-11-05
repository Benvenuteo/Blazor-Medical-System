namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class PatientDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public bool ProfileIncomplete { get; set; }
        public bool HasPassword { get; set; }
        public bool EmailConfirmed { get; set; }
    }

}

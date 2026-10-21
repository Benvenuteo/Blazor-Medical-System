using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class RegisterPatientDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateTime DateOfBirth { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;
    }
}

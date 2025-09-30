using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class RegisterPatientDto
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        public string PhoneNumber { get; set; }
    }
}

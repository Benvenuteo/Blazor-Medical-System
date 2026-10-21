using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class CreatePrescriptionDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public string Medication { get; set; } = string.Empty;

        [Required]
        public string Dosage { get; set; } = string.Empty;

        public string Instructions { get; set; } = string.Empty;

        [Range(1, 365)]
        public int ValidityDays { get; set; } = 30;
    }
}

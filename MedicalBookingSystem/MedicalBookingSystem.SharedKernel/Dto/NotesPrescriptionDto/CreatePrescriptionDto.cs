using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class CreatePrescriptionDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public string Medication { get; set; }

        [Required]
        public string Dosage { get; set; }

        public string Instructions { get; set; }

        [Range(1, 365)]
        public int ValidityDays { get; set; } = 30;
    }
}

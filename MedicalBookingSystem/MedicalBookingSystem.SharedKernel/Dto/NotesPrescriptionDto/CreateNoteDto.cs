using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class CreateNoteDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required, MinLength(10)]
        public string Content { get; set; }
    }

}

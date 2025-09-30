using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class AppointmentDetailsDto : AppointmentDto
    {
        public NoteDto? Note { get; set; }
        public PrescriptionDto? Prescription { get; set; }
        public PatientBasicDto? Patient { get; set; }
        public DoctorDto? Doctor { get; set; }
    }
}

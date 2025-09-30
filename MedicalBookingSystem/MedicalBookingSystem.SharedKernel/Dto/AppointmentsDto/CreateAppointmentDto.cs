namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class CreateAppointmentDto
    {
        public DateTime Date { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
    }
}

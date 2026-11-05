namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class CreateAppointmentDto
    {
        public DateTime Date { get; set; }
        public int DoctorId { get; set; }
        /// <summary>Teleporada (video) instead of a visit at the clinic.</summary>
        public bool Remote { get; set; }
    }
}

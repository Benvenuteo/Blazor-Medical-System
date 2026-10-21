namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class AppointmentDto
    {
        public int Id { get; set; }
        public bool HasNote { get; set; }
        public bool HasPrescription { get; set; }
        public DateTime Date { get; set; }
        public int PatientId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

}

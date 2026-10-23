namespace MedicalBookingSystem.Domain.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public Guid Version { get; set; } = Guid.NewGuid();
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public Note? Note { get; set; }
        public Prescription? Prescription { get; set; }
    }

    public enum AppointmentStatus
    {
        Scheduled,
        Completed,
        Cancelled
    }
}

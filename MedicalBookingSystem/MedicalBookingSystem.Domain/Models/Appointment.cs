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
        /// <summary>Set once the day-before reminder is queued, so it is never sent twice.</summary>
        public DateTime? ReminderQueuedAtUtc { get; set; }
        /// <summary>Copied from the schedule when booked, so later schedule edits do not rewrite history.</summary>
        public int? ClinicId { get; set; }
        public Clinic? Clinic { get; set; }

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

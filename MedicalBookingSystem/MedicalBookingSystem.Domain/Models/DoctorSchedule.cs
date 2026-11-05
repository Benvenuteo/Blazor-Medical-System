namespace MedicalBookingSystem.Domain.Models
{
    public class DoctorSchedule
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; } = true;
        /// <summary>Where this shift takes place. Null only for schedules created before clinics existed.</summary>
        public int? ClinicId { get; set; }
        public Clinic? Clinic { get; set; }

        // Relacja
        public Doctor Doctor { get; set; } = null!;
    }
}

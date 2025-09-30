namespace MedicalBookingSystem.Domain.Models
{
    public class DoctorSchedule
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; } = true;

        // Relacja
        public Doctor Doctor { get; set; }
    }
}

namespace MedicalBookingSystem.Domain.Models
{
    public class Note
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int AppointmentId { get; set; }

        // Relacja
        public Appointment Appointment { get; set; }
    }
}

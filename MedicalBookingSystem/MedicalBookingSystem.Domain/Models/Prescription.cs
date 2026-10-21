namespace MedicalBookingSystem.Domain.Models
{
    public class Prescription
    {
        public int Id { get; set; }
        public string Medication { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryDate { get; set; }
        public int AppointmentId { get; set; }

        // Relacja
        public Appointment Appointment { get; set; } = null!;
    }
}

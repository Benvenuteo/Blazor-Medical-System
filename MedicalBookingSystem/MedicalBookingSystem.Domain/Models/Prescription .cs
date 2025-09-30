namespace MedicalBookingSystem.Domain.Models
{
    public class Prescription
    {
        public int Id { get; set; }
        public string Medication { get; set; }
        public string Dosage { get; set; }
        public string Instructions { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryDate { get; set; }
        public int AppointmentId { get; set; }

        // Relacja
        public Appointment Appointment { get; set; }
    }
}

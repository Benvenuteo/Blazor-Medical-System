namespace MedicalBookingSystem.Domain.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public Doctor Doctor { get; set; }
        public Patient Patient { get; set; }
    }
}

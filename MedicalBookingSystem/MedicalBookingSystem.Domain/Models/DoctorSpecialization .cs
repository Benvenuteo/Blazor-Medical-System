namespace MedicalBookingSystem.Domain.Models
{
    public class DoctorSpecialization
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public int SpecializationId { get; set; }

        // Relacje
        public Doctor Doctor { get; set; }
        public Specialization Specialization { get; set; }
    }
}

namespace MedicalBookingSystem.Domain.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = "/images/no-image-icon.svg";
        public decimal AverageRating { get; private set; }

        // Relacje
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();

        // Metoda do aktualizacji średniej oceny
        public void UpdateAverageRating()
        {
            AverageRating = Reviews.Count == 0 ? 0 : (decimal)Reviews.Average(review => review.Rating);
        }
    }
}

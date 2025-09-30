namespace MedicalBookingSystem.Domain.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string LicenseNumber { get; set; }
        public string Region { get; set; }
        public string Bio { get; set; }
        public string ImageUrl { get; set; } = "/images/no-image-icon.png";
        public decimal AverageRating { get; private set; }

        // Relacje
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();

        // Metoda do aktualizacji średniej oceny
        public void UpdateAverageRating()
        {
            if (Reviews?.Any() == true)
            {
                AverageRating = (decimal)Reviews.Average(r => r.Rating);
            }
        }
    }
}

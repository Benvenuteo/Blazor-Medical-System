namespace MedicalBookingSystem.Domain.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public Guid SecurityStamp { get; set; } = Guid.NewGuid();
        public Guid Version { get; set; } = Guid.NewGuid();
        public DateTime DateOfBirth { get; set; }
        /// <summary>Public demonstration account: its credentials, profile and sessions cannot be changed.</summary>
        public bool IsDemo { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }

        // Relacje
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}

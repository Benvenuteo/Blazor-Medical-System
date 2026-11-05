namespace MedicalBookingSystem.Domain.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        /// <summary>Null for accounts created through an external provider until a password is set.</summary>
        public string? PasswordHash { get; set; }
        public Guid SecurityStamp { get; set; } = Guid.NewGuid();
        public Guid Version { get; set; } = Guid.NewGuid();
        /// <summary>Null only while <see cref="ProfileIncomplete"/> is true (external sign-up).</summary>
        public DateTime? DateOfBirth { get; set; }
        /// <summary>Set for external sign-ups until birth date and phone are provided; booking is blocked meanwhile.</summary>
        public bool ProfileIncomplete { get; set; }
        /// <summary>Public demonstration account: its credentials, profile and sessions cannot be changed.</summary>
        public bool IsDemo { get; set; }
        public bool EmailConfirmed { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }

        // Relacje
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}

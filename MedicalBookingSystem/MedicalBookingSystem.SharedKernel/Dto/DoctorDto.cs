namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class DoctorDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime? NextAvailableSlot { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        /// <summary>Nearest clinic within the search radius (or the first clinic without a search centre).</summary>
        public int? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public string? ClinicAddress { get; set; }
        public double? ClinicLatitude { get; set; }
        public double? ClinicLongitude { get; set; }
        public double? DistanceKm { get; set; }
    }

}

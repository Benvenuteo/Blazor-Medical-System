namespace MedicalBookingSystem.SharedKernel.Dto.DtoHelp
{
    public class SearchDoctorsDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? SpecializationId { get; set; }
        public string? Region { get; set; }
        public DateTime? Date { get; set; }
        public string? SortBy { get; set; } // "rating", "availability" or "distance"
        /// <summary>Search centre; both coordinates or none. The UI rounds them to about 1 km.</summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
    }
}

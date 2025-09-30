namespace MedicalBookingSystem.SharedKernel.Dto.DtoHelp
{
    public class SearchDoctorsDto
    {
        public int? SpecializationId { get; set; }
        public string? Region { get; set; }
        public DateTime? Date { get; set; }
        public string? SortBy { get; set; } // "rating", "distance", "availability"
    }
}

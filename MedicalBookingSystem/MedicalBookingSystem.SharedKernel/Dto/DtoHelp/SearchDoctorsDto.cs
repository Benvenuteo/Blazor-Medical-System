namespace MedicalBookingSystem.SharedKernel.Dto.DtoHelp
{
    public class SearchDoctorsDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? SpecializationId { get; set; }
        public string? Region { get; set; }
        public DateTime? Date { get; set; }
        public string? SortBy { get; set; } // "rating" or "availability"
    }
}

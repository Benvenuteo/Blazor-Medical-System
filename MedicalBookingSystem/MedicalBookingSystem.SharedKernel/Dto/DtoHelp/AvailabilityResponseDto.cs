namespace MedicalBookingSystem.SharedKernel.Dto.DtoHelp
{
    public class AvailabilityResponseDto
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; }
        public List<DateTime> AvailableSlots { get; set; } = new();
    }

}

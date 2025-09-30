namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class DoctorDetailsDto : DoctorDto
    {
        public List<SpecializationDto> Specializations { get; set; } = new();
        public List<DoctorScheduleDto> Schedules { get; set; } = new();
    }
}

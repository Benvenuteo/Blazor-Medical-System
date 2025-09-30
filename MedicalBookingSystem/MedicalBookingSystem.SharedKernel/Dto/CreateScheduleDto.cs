namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class CreateScheduleDto
    {
        public int DoctorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}

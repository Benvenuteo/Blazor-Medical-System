namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class CreateScheduleDto
    {
        public int DoctorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        /// <summary>Required when the doctor works at one or more clinics.</summary>
        public int? ClinicId { get; set; }
    }
}

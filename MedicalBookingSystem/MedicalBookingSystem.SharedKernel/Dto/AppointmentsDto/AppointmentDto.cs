namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class AppointmentDto
    {
        public int Id { get; set; }
        public bool HasNote { get; set; }
        public bool HasPrescription { get; set; }
        public DateTime Date { get; set; }
        public int PatientId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ClinicName { get; set; }
        public string? ClinicAddress { get; set; }
        public double? ClinicLatitude { get; set; }
        public double? ClinicLongitude { get; set; }
        public bool IsRemote { get; set; }
        public string? VideoLink { get; set; }
    }

}

namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

public sealed class AvailableSlotDto
{
    public int DoctorId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string? ClinicAddress { get; set; }
}

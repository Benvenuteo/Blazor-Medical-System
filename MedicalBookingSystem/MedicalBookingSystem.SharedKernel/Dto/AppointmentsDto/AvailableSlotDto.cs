namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

public sealed class AvailableSlotDto
{
    public int DoctorId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

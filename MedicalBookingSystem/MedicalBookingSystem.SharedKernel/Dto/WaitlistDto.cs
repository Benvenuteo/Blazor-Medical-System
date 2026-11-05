namespace MedicalBookingSystem.SharedKernel.Dto;

public sealed class JoinWaitlistDto
{
    public int DoctorId { get; set; }
    /// <summary>Notify about free slots starting before this local date and time.</summary>
    public DateTime Before { get; set; }
}

public sealed record WaitlistEntryDto(int Id, int DoctorId, string DoctorName, DateTime Before, DateTime CreatedAtUtc);

namespace MedicalBookingSystem.SharedKernel.Dto;

public sealed record AuthenticationResponseDto(string Token, PatientDto Patient);
public sealed record TokenResponseDto(string Token);

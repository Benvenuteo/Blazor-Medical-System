namespace MedicalBookingSystem.Application.Security;

public sealed class AccessDeniedException(bool authenticated, string? message = null) : Exception(message ?? "Brak dostępu do zasobu.")
{
    public bool Authenticated { get; } = authenticated;
}

namespace MedicalBookingSystem.Application.Security;

public sealed class AccessDeniedException(bool authenticated) : Exception("Brak dostępu do zasobu.")
{
    public bool Authenticated { get; } = authenticated;
}

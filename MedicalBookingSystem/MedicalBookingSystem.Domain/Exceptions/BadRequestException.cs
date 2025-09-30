namespace MedicalBookingSystem.Domain.Exceptions
{
    // Wyjątek: żądanie użytkownika zawiera błędy
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message) { }
    }
}

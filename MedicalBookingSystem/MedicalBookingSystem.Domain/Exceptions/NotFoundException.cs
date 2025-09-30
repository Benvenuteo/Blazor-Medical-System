namespace MedicalBookingSystem.Domain.Exceptions
{
    // Wyjątek: Obiekt nie został znaleziony
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}

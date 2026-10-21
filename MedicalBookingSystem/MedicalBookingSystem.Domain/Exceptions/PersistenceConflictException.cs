namespace MedicalBookingSystem.Domain.Exceptions;

public enum PersistenceConflictKind { ConcurrentChange, AppointmentSlot, ScheduleOverlap, DuplicateReview, DuplicateDocument }

/// <summary>A storage-enforced business conflict, independent of the database provider.</summary>
public sealed class PersistenceConflictException(PersistenceConflictKind kind, Exception innerException)
    : Exception("The operation conflicts with data already saved by another request.", innerException)
{
    public PersistenceConflictKind Kind { get; } = kind;
}

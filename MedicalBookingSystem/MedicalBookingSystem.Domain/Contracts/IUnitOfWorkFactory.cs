namespace MedicalBookingSystem.Domain.Contracts;

/// <summary>Creates an independently owned unit of work for a single application operation.</summary>
public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}

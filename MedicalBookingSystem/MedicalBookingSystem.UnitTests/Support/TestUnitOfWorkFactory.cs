using MedicalBookingSystem.Domain.Contracts;

namespace MedicalBookingSystem.UnitTests.Support;

internal sealed class TestUnitOfWorkFactory(IUnitOfWork unit) : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => unit;
}

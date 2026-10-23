using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure;

public sealed class UnitOfWorkFactory(IDbContextFactory<ApplicationDbContext> contexts, ClinicClock clock) : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => new UnitOfWork(contexts.CreateDbContext(), clock);
}

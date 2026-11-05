namespace MedicalBookingSystem.Infrastructure.Repositories;

/// <summary>Plain repository for entities that need no custom queries.</summary>
public sealed class EntityRepository<TEntity>(ApplicationDbContext context) : Repository<TEntity>(context) where TEntity : class;

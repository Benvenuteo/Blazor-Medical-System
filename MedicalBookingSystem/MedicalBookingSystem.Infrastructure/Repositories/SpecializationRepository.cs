using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class SpecializationRepository : Repository<Specialization>, ISpecializationRepository
    {
        public SpecializationRepository(ApplicationDbContext context) : base(context) { }
    }
}

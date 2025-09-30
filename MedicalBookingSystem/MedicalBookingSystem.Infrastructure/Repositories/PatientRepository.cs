using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class PatientRepository : Repository<Patient>, IPatientRepository
    {
        public PatientRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Patient> GetByEmailAsync(string email)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email == email);
        }
    }
}

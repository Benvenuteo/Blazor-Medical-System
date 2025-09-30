using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class PrescriptionRepository : Repository<Prescription>, IPrescriptionRepository
    {
        public PrescriptionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Prescription>> GetByPatientIdAsync(int patientId)
        {
            return await _dbSet
                .Include(p => p.Appointment)
                .ThenInclude(a => a.Doctor)
                .Where(p => p.Appointment.PatientId == patientId)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}

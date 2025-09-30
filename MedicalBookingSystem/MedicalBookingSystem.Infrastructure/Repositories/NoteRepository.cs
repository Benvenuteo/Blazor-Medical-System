using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class NoteRepository : Repository<Note>, INoteRepository
    {
        public NoteRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Note>> GetByPatientIdAsync(int patientId)
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

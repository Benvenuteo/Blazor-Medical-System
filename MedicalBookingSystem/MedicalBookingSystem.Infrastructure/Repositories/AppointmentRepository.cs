using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Repositories
{
    public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
    {
        private readonly ClinicClock _clock;
        public AppointmentRepository(ApplicationDbContext context, ClinicClock clock) : base(context) => _clock = clock;





        public async Task<Appointment?> GetAppointmentByIdAsync(int id)
        {
            return await _dbSet
                    .Where(a => a.Id == id)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.DoctorSpecializations)
                            .ThenInclude(ds => ds.Specialization)
                    .Include(a => a.Note)
                    .Include(a => a.Prescription)
                    .Include(a => a.Patient)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
        }




    }
}

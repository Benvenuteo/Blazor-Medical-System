using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure.Repositories;

namespace MedicalBookingSystem.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Patients = new PatientRepository(context);
            Doctors = new DoctorRepository(context);
            Appointments = new AppointmentRepository(context);
            Reviews = new ReviewRepository(context);
            DoctorSchedules = new DoctorScheduleRepository(context);
            Prescriptions = new PrescriptionRepository(context);
            Notes = new NoteRepository(context);
            Specializations = new SpecializationRepository(context);
        }

        public IPatientRepository Patients { get; }
        public IDoctorRepository Doctors { get; }
        public IAppointmentRepository Appointments { get; }
        public IReviewRepository Reviews { get; }
        public IDoctorScheduleRepository DoctorSchedules { get; }
        public IPrescriptionRepository Prescriptions { get; }
        public INoteRepository Notes { get; }
        public ISpecializationRepository Specializations { get; }

        public async Task<int> SaveChangesAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task BeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }
            await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await _context.Database.CommitTransactionAsync();
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            await _context.Database.RollbackTransactionAsync();
        }

        public void Dispose()
        {
            _context.Database.CurrentTransaction?.Dispose();
            _context.Dispose();
        }
    }
}

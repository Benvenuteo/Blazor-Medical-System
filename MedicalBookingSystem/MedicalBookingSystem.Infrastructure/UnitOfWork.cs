using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Infrastructure.Repositories;

namespace MedicalBookingSystem.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context, ClinicClock clock)
        {
            _context = context;
            Patients = new PatientRepository(context);
            StaffAccounts = new StaffAccountRepository(context);
            Doctors = new DoctorRepository(context);
            Appointments = new AppointmentRepository(context, clock);
            Reviews = new ReviewRepository(context);
            DoctorSchedules = new DoctorScheduleRepository(context);
            Prescriptions = new PrescriptionRepository(context);
            Notes = new NoteRepository(context);
            Specializations = new SpecializationRepository(context);
        }

        public IPatientRepository Patients { get; }
        public IRepository<MedicalBookingSystem.Domain.Models.StaffAccount> StaffAccounts { get; }
        public IDoctorRepository Doctors { get; }
        public IAppointmentRepository Appointments { get; }
        public IReviewRepository Reviews { get; }
        public IDoctorScheduleRepository DoctorSchedules { get; }
        public IPrescriptionRepository Prescriptions { get; }
        public INoteRepository Notes { get; }
        public ISpecializationRepository Specializations { get; }

        public async Task<int> SaveChangesAsync()
        {
            try { return await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException exception)
            {
                _context.ChangeTracker.Clear();
                throw new PersistenceConflictException(PersistenceConflictKind.ConcurrentChange, exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqlite &&
                sqlite.SqliteExtendedErrorCode is 2067 or 1811)
            {
                var kind = sqlite.SqliteExtendedErrorCode == 1811 && sqlite.Message.Contains("last_active_administrator", StringComparison.Ordinal) ? PersistenceConflictKind.LastAdministrator
                    : exception.Entries.Any(e => e.Entity is Appointment) ? PersistenceConflictKind.AppointmentSlot
                    : exception.Entries.Any(e => e.Entity is DoctorSchedule) ? PersistenceConflictKind.ScheduleOverlap
                    : exception.Entries.Any(e => e.Entity is Review) ? PersistenceConflictKind.DuplicateReview
                    : exception.Entries.Any(e => e.Entity is Note or Prescription) ? PersistenceConflictKind.DuplicateDocument
                    : exception.Entries.Any(e => e.Entity is Patient or StaffAccount) ? PersistenceConflictKind.DuplicateAccount
                    : (PersistenceConflictKind?)null;
                if (kind is null) throw;
                // A rejected entity must not be written accidentally by a later action in the same circuit.
                _context.ChangeTracker.Clear();
                throw new PersistenceConflictException(kind.Value, exception);
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

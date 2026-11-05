namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IClinicalAuditWriter ClinicalAudit { get; }
        IPatientRepository Patients { get; }
        IRepository<MedicalBookingSystem.Domain.Models.StaffAccount> StaffAccounts { get; }
        IDoctorRepository Doctors { get; }
        IAppointmentRepository Appointments { get; }
        IReviewRepository Reviews { get; }
        IDoctorScheduleRepository DoctorSchedules { get; }
        IPrescriptionRepository Prescriptions { get; }
        INoteRepository Notes { get; }
        ISpecializationRepository Specializations { get; }
        IRepository<MedicalBookingSystem.Domain.Models.EmailMessage> EmailOutbox { get; }
        IRepository<MedicalBookingSystem.Domain.Models.AccountToken> AccountTokens { get; }
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}

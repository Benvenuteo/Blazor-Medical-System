namespace MedicalBookingSystem.Domain.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IPatientRepository Patients { get; }
        IDoctorRepository Doctors { get; }
        IAppointmentRepository Appointments { get; }
        IReviewRepository Reviews { get; }
        IDoctorScheduleRepository DoctorSchedules { get; }
        IPrescriptionRepository Prescriptions { get; }
        INoteRepository Notes { get; }
        ISpecializationRepository Specializations { get; }
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}

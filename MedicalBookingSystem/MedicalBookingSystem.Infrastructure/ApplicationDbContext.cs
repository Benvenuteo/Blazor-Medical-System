using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

        public DbSet<ClinicalAuditEntry> ClinicalAudit { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<StaffAccount> StaffAccounts { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<EmailMessage> EmailOutbox { get; set; }
        public DbSet<AccountToken> AccountTokens { get; set; }
        public DbSet<PatientExternalLogin> PatientExternalLogins { get; set; }
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<DoctorClinic> DoctorClinics { get; set; }
        public DbSet<WaitlistEntry> Waitlist { get; set; }
        public DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }

        private void StampAccountVersions()
        {
            foreach (var entry in ChangeTracker.Entries<Patient>().Where(e => e.State is EntityState.Added or EntityState.Modified))
                entry.Property(a => a.Version).CurrentValue = Guid.NewGuid();
            foreach (var entry in ChangeTracker.Entries<StaffAccount>().Where(e => e.State is EntityState.Added or EntityState.Modified))
                entry.Property(a => a.Version).CurrentValue = Guid.NewGuid();
        }

        // SQLite has no rowversion. Every appointment write gets an application-managed concurrency token.
        private void StampAppointmentVersions()
        {
            foreach (var entry in ChangeTracker.Entries<Appointment>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified))
                entry.Property(a => a.Version).CurrentValue = Guid.NewGuid();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampAppointmentVersions();
            StampAccountVersions();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampAppointmentVersions();
            StampAccountVersions();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClinicalAuditEntry>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Actor).HasMaxLength(60).IsRequired();
                entity.Property(a => a.ActorRole).HasMaxLength(20).IsRequired();
                entity.Property(a => a.Action).HasMaxLength(20).IsRequired();
                entity.Property(a => a.Resource).HasMaxLength(40).IsRequired();
                entity.HasIndex(a => new { a.OccurredAtUtc, a.Id });
                entity.HasIndex(a => new { a.PatientId, a.OccurredAtUtc });
            });
            modelBuilder.Entity<StaffAccount>(entity =>
            {
                entity.ToTable("StaffAccounts", table => table.HasCheckConstraint("CK_StaffAccount_Role",
                    "(Role = 'Doctor' AND DoctorId IS NOT NULL) OR (Role = 'Admin' AND DoctorId IS NULL)"));
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Version).IsConcurrencyToken();
                entity.Property(a => a.Email).IsRequired().HasMaxLength(255).UseCollation("NOCASE");
                entity.HasIndex(a => a.Email).IsUnique();
                entity.Property(a => a.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(a => a.PasswordHash).IsRequired();
                entity.Property(a => a.Role).HasConversion<string>().HasMaxLength(20);
                entity.HasIndex(a => a.DoctorId).IsUnique();
                entity.HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmailMessage>(entity =>
            {
                entity.ToTable("EmailOutbox");
                entity.Property(m => m.To).IsRequired().HasMaxLength(255);
                entity.Property(m => m.Subject).IsRequired().HasMaxLength(200);
                entity.Property(m => m.AttachmentName).HasMaxLength(100);
                entity.Property(m => m.AttachmentContentType).HasMaxLength(100);
                entity.Property(m => m.LastError).HasMaxLength(1000);
                entity.HasIndex(m => new { m.SentAtUtc, m.NextAttemptAtUtc });
            });
            modelBuilder.Entity<Clinic>(entity =>
            {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(150).UseCollation("NOCASE");
                entity.HasIndex(c => c.Name).IsUnique();
                entity.Property(c => c.Street).IsRequired().HasMaxLength(200);
                entity.Property(c => c.City).IsRequired().HasMaxLength(100);
                entity.Property(c => c.PostalCode).HasMaxLength(10);
                entity.Property(c => c.Phone).HasMaxLength(20);
                entity.Ignore(c => c.Address);
                // Supports the bounding-box pre-filter of the radius search.
                entity.HasIndex(c => new { c.Latitude, c.Longitude });
            });
            modelBuilder.Entity<WaitlistEntry>(entity =>
            {
                entity.ToTable("WaitlistEntries");
                entity.HasOne(e => e.Patient).WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Doctor).WithMany().HasForeignKey(e => e.DoctorId).OnDelete(DeleteBehavior.Cascade);
                // One open entry per patient and doctor; closed entries stay as history.
                entity.HasIndex(e => new { e.PatientId, e.DoctorId }).IsUnique().HasFilter("Active = 1");
                entity.HasIndex(e => new { e.DoctorId, e.Active, e.Before });
            });
            modelBuilder.Entity<DoctorClinic>(entity =>
            {
                entity.HasKey(dc => new { dc.DoctorId, dc.ClinicId });
                entity.HasOne(dc => dc.Doctor).WithMany(d => d.DoctorClinics).HasForeignKey(dc => dc.DoctorId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(dc => dc.Clinic).WithMany(c => c.DoctorClinics).HasForeignKey(dc => dc.ClinicId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(dc => dc.ClinicId);
            });
            modelBuilder.Entity<DoctorSchedule>().HasOne(s => s.Clinic).WithMany().HasForeignKey(s => s.ClinicId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Appointment>().HasOne(a => a.Clinic).WithMany().HasForeignKey(a => a.ClinicId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Appointment>().Property(a => a.VideoRoom).HasMaxLength(100);
            modelBuilder.Entity<PatientExternalLogin>(entity =>
            {
                entity.Property(l => l.Provider).IsRequired().HasMaxLength(30);
                entity.Property(l => l.Subject).IsRequired().HasMaxLength(255);
                entity.Property(l => l.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();
                entity.HasIndex(l => new { l.PatientId, l.Provider }).IsUnique();
                entity.HasOne(l => l.Patient).WithMany().HasForeignKey(l => l.PatientId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AccountToken>(entity =>
            {
                entity.Property(t => t.Purpose).HasConversion<string>().HasMaxLength(30);
                entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
                entity.HasIndex(t => t.TokenHash).IsUnique();
                entity.HasIndex(t => new { t.PatientId, t.Purpose });
                entity.HasOne(t => t.Patient).WithMany().HasForeignKey(t => t.PatientId).OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja encji Patient
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Version).IsConcurrencyToken();
                entity.Property(p => p.Email).IsRequired().HasMaxLength(255).UseCollation("NOCASE");
                entity.HasIndex(p => p.Email).IsUnique();
                entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
                // Optional since external sign-up; ProfileIncomplete gates the missing birth date.
                entity.Property(p => p.PasswordHash);
                entity.Property(p => p.DateOfBirth);
            });

            // Konfiguracja encji Doctor
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.LastName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.LicenseNumber).IsRequired().HasMaxLength(50);
                entity.HasIndex(d => d.LicenseNumber).IsUnique();
                entity.Property(d => d.Region).HasMaxLength(100);
                entity.Property(d => d.Bio).HasColumnType("text");
            });

            // Konfiguracja encji Appointment
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Version).IsConcurrencyToken();
                entity.Property(a => a.Date).IsRequired();
                entity.HasIndex(a => new { a.PatientId, a.Status, a.Date, a.Id });
                entity.HasIndex(a => new { a.DoctorId, a.Status, a.Date, a.Id });
                entity.Property(a => a.Version).IsConcurrencyToken();
                entity.HasIndex(a => new { a.DoctorId, a.Date }).IsUnique().HasFilter("Status <> 'Cancelled'");
                entity.HasIndex(a => new { a.PatientId, a.Date }).IsUnique().HasFilter("Status <> 'Cancelled'");
                entity.Property(a => a.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Relacje
                entity.HasOne(a => a.Patient)
                    .WithMany(p => p.Appointments)
                    .HasForeignKey(a => a.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Doctor)
                    .WithMany(d => d.Appointments)
                    .HasForeignKey(a => a.DoctorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Konfiguracja encji Note
            modelBuilder.Entity<Note>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Content).IsRequired().HasColumnType("text");
                entity.Property(n => n.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relacja 1:1 z Appointment
                entity.HasOne(n => n.Appointment)
                    .WithOne(a => a.Note)
                    .HasForeignKey<Note>(n => n.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja encji Prescription
            modelBuilder.Entity<Prescription>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Medication).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Dosage).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Instructions).HasColumnType("text");
                entity.Property(p => p.CreatedDate).IsRequired();
                entity.Property(p => p.ExpiryDate).IsRequired();

                // Relacja 1:1 z Appointment
                entity.HasOne(p => p.Appointment)
                    .WithOne(a => a.Prescription)
                    .HasForeignKey<Prescription>(p => p.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja encji Review
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => new { r.PatientId, r.DoctorId }).IsUnique();
                entity.Property(r => r.Rating).IsRequired();
                entity.HasIndex(r => new { r.DoctorId, r.CreatedDate, r.Id });
                entity.Property(r => r.Comment).HasColumnType("text");
                entity.Property(r => r.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relacje
                entity.HasOne(r => r.Doctor)
                    .WithMany(d => d.Reviews)
                    .HasForeignKey(r => r.DoctorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Patient)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(r => r.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Konfiguracja encji Specialization
            modelBuilder.Entity<Specialization>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(s => s.Name).IsUnique();
                entity.Property(s => s.Description).HasColumnType("text");
            });

            // Konfiguracja encji DoctorSchedule
            modelBuilder.Entity<DoctorSchedule>(entity =>
            {
                entity.HasKey(ds => ds.Id);
                entity.Property(ds => ds.StartTime).IsRequired();
                entity.Property(ds => ds.EndTime).IsRequired();
                entity.Property(ds => ds.IsAvailable).HasDefaultValue(true);

                // Relacja z Doctor
                entity.HasOne(ds => ds.Doctor)
                    .WithMany(d => d.Schedules)
                    .HasForeignKey(ds => ds.DoctorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja encji DoctorSpecialization (relacja wiele-do-wielu)
            modelBuilder.Entity<DoctorSpecialization>(entity =>
            {
                entity.HasKey(ds => new { ds.DoctorId, ds.SpecializationId });

                // Relacja z Doctor
                entity.HasOne(ds => ds.Doctor)
                    .WithMany(d => d.DoctorSpecializations)
                    .HasForeignKey(ds => ds.DoctorId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relacja z Specialization
                entity.HasOne(ds => ds.Specialization)
                    .WithMany(s => s.DoctorSpecializations)
                    .HasForeignKey(ds => ds.SpecializationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}

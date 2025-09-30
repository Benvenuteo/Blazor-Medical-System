using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Konfiguracja encji Patient
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(p => p.Email).IsUnique();
                entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.PasswordHash).IsRequired();
                entity.Property(p => p.DateOfBirth).IsRequired();
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
                entity.Property(a => a.Date).IsRequired();
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
                entity.Property(r => r.Rating).IsRequired();
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

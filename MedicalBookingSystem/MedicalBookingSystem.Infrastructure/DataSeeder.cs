using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Infrastructure;

/// <summary>Creates synthetic, repeatable local demo data; never runs unless explicitly enabled.</summary>
public sealed class DataSeeder(
    ApplicationDbContext context,
    ClinicClock clock,
    IPasswordHasher<Patient> passwordHasher,
    IPasswordHasher<StaffAccount> staffHasher,
    IOptions<DemoDataOptions> options)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return;
        if (string.IsNullOrWhiteSpace(options.Value.PatientPassword) || string.IsNullOrWhiteSpace(options.Value.StaffPassword))
            throw new InvalidOperationException("Set Seed__PatientPassword and Seed__StaffPassword before enabling demonstration data.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var doctor = await context.Doctors.SingleOrDefaultAsync(d => d.LicenseNumber == "DEMO-001", cancellationToken);
        if (doctor is null && !await context.Doctors.AnyAsync(cancellationToken))
        {
            var specialization = new Specialization { Name = "Kardiologia", Description = "Przykładowa specjalizacja" };
            doctor = new Doctor
            {
                FirstName = "Anna", LastName = "Demonstracyjna", LicenseNumber = "DEMO-001",
                Region = "Małopolskie", Bio = "Fikcyjna osoba używana wyłącznie w demonstracji aplikacji."
            };
            doctor.DoctorSpecializations.Add(new DoctorSpecialization { Doctor = doctor, Specialization = specialization });
            for (var offset = 2; offset <= 14; offset++)
            {
                doctor.Schedules.Add(new DoctorSchedule
                {
                    Doctor = doctor, StartTime = clock.Today.AddDays(offset).AddHours(9),
                    EndTime = clock.Today.AddDays(offset).AddHours(16), IsAvailable = true
                });
            }
            var patient = new Patient
            {
                FirstName = "Jan", LastName = "Demonstracyjny", Email = "patient@example.test",
                PhoneNumber = "+48500000000", DateOfBirth = new DateTime(1990, 1, 1)
            };
            patient.PasswordHash = passwordHasher.HashPassword(patient, options.Value.PatientPassword);
            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync(cancellationToken);
            context.Appointments.Add(new Appointment
            {
                DoctorId = doctor.Id, PatientId = patient.Id, Date = clock.Today.AddDays(-2).AddHours(10),
                Status = AppointmentStatus.Completed
            });
            context.Appointments.Add(new Appointment
            {
                DoctorId = doctor.Id, PatientId = patient.Id, Date = clock.Today.AddDays(-1).AddHours(11),
                Status = AppointmentStatus.Scheduled
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        // Upgrade an existing synthetic demo without touching real doctors or replacing credentials.
        if (doctor is not null && !await context.StaffAccounts.AnyAsync(cancellationToken))
        {
            var doctorAccount = new StaffAccount
            {
                Email = "doctor@example.test", DisplayName = "Anna Demonstracyjna", Role = StaffRole.Doctor, DoctorId = doctor.Id
            };
            var adminAccount = new StaffAccount
            {
                Email = "admin@example.test", DisplayName = "Administrator demonstracyjny", Role = StaffRole.Admin
            };
            foreach (var account in new[] { doctorAccount, adminAccount })
            {
                account.PasswordHash = staffHasher.HashPassword(account, options.Value.StaffPassword);
                context.StaffAccounts.Add(account);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class DemoDataOptions
{
    public bool Enabled { get; set; }
    public string PatientPassword { get; set; } = string.Empty;
    public string StaffPassword { get; set; } = string.Empty;
}

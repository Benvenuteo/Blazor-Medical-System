using MedicalBookingSystem.Application.Security;
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
        var doctor = await context.Doctors.SingleOrDefaultAsync(d => d.LicenseNumber == DemoDoctorLicense, cancellationToken);
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
                FirstName = "Jan", LastName = "Demonstracyjny", Email = DemoAccounts.PatientEmail,
                PhoneNumber = "+48500000000", DateOfBirth = new DateTime(1990, 1, 1), IsDemo = true, EmailConfirmed = true
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
                Email = DemoAccounts.DoctorEmail, DisplayName = "Anna Demonstracyjna", Role = StaffRole.Doctor, DoctorId = doctor.Id, IsDemo = true
            };
            var adminAccount = new StaffAccount
            {
                Email = DemoAccounts.AdminEmail, DisplayName = "Administrator demonstracyjny", Role = StaffRole.Admin, IsDemo = true
            };
            foreach (var account in new[] { doctorAccount, adminAccount })
            {
                account.PasswordHash = staffHasher.HashPassword(account, options.Value.StaffPassword);
                context.StaffAccounts.Add(account);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        // Demo databases created before the flag existed: protect the well-known public accounts.
        await context.Patients.Where(p => p.Email == DemoAccounts.PatientEmail && !p.IsDemo)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDemo, true), cancellationToken);
        await context.StaffAccounts.Where(a => DemoAccounts.StaffEmails.Contains(a.Email) && !a.IsDemo)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDemo, true), cancellationToken);
        await EnsureDemoNetworkAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TopUpScheduleAsync(cancellationToken);
    }

    private sealed record DemoClinic(string Name, string Street, string PostalCode, string City, double Latitude, double Longitude);
    private sealed record DemoDoctor(string License, string FirstName, string LastName, string Specialization, string Region, int Clinic, string Bio);

    // Fictional clinics and doctors placed in real cities, so the radius search has something to show:
    // three are within 30 km of Kraków, Katowice is about 70 km away and Warsaw about 250 km.
    private static readonly DemoClinic[] DemoClinics =
    [
        new("Centrum Medyczne Demo — Kraków Stare Miasto", "ul. Floriańska 10", "31-019", "Kraków", 50.0631, 19.9395),
        new("Przychodnia Demo — Kraków Bronowice", "ul. Rydla 40", "30-087", "Kraków", 50.0810, 19.8920),
        new("Gabinet Demo — Wieliczka", "Rynek Górny 3", "32-020", "Wieliczka", 49.9871, 20.0640),
        new("Centrum Zdrowia Demo — Katowice", "ul. Mariacka 15", "40-014", "Katowice", 50.2584, 19.0240),
        new("Klinika Demo — Warszawa", "ul. Nowy Świat 20", "00-373", "Warszawa", 52.2329, 21.0181),
    ];

    private static readonly DemoDoctor[] DemoDoctors =
    [
        new(DemoDoctorLicense, "Anna", "Demonstracyjna", "Kardiologia", "Małopolskie", 0, "Fikcyjna osoba używana wyłącznie w demonstracji aplikacji."),
        new("DEMO-002", "Piotr", "Przykładowy", "Dermatologia", "Małopolskie", 1, "Fikcyjny dermatolog z danych demonstracyjnych."),
        new("DEMO-003", "Ewa", "Testowa", "Pediatria", "Małopolskie", 2, "Fikcyjna pediatra z danych demonstracyjnych."),
        new("DEMO-004", "Marek", "Fikcyjny", "Kardiologia", "Śląskie", 3, "Fikcyjny kardiolog z danych demonstracyjnych."),
        new("DEMO-005", "Zofia", "Wzorcowa", "Neurologia", "Mazowieckie", 4, "Fikcyjna neurolog z danych demonstracyjnych."),
    ];

    /// <summary>Adds the demo clinics and doctors that are missing; existing records and real data are left untouched.</summary>
    private async Task EnsureDemoNetworkAsync(CancellationToken cancellationToken)
    {
        var demo = await context.Doctors.SingleOrDefaultAsync(d => d.LicenseNumber == DemoDoctorLicense, cancellationToken);
        if (demo is null) return; // a database with real doctors and no demo doctor stays as it is
        var clinics = new List<Clinic>();
        foreach (var definition in DemoClinics)
        {
            var clinic = await context.Clinics.SingleOrDefaultAsync(c => c.Name == definition.Name, cancellationToken);
            if (clinic is null)
            {
                clinic = new Clinic { Name = definition.Name, Street = definition.Street, PostalCode = definition.PostalCode,
                    City = definition.City, Latitude = definition.Latitude, Longitude = definition.Longitude, Phone = "+48120000000" };
                context.Clinics.Add(clinic);
            }
            clinics.Add(clinic);
        }
        foreach (var definition in DemoDoctors)
        {
            var specialization = await context.Specializations.SingleOrDefaultAsync(s => s.Name == definition.Specialization, cancellationToken)
                ?? context.Specializations.Local.SingleOrDefault(s => s.Name == definition.Specialization)
                ?? context.Specializations.Add(new Specialization { Name = definition.Specialization, Description = "Przykładowa specjalizacja" }).Entity;
            var doctor = definition.License == DemoDoctorLicense ? demo
                : await context.Doctors.SingleOrDefaultAsync(d => d.LicenseNumber == definition.License, cancellationToken);
            if (doctor is null)
            {
                doctor = new Doctor { FirstName = definition.FirstName, LastName = definition.LastName, LicenseNumber = definition.License,
                    Region = definition.Region, Bio = definition.Bio };
                doctor.DoctorSpecializations.Add(new DoctorSpecialization { Doctor = doctor, Specialization = specialization });
                context.Doctors.Add(doctor);
            }
            var clinic = clinics[definition.Clinic];
            if (doctor.Id == 0 || clinic.Id == 0 ||
                !await context.DoctorClinics.AnyAsync(l => l.DoctorId == doctor.Id && l.ClinicId == clinic.Id, cancellationToken))
                context.DoctorClinics.Add(new DoctorClinic { Doctor = doctor, Clinic = clinic });
        }
        await context.SaveChangesAsync(cancellationToken);
        // Shifts created before clinics existed belong to the demo doctor's clinic.
        var demoClinicId = clinics[0].Id;
        await context.DoctorSchedules.Where(s => s.DoctorId == demo.Id && s.ClinicId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClinicId, demoClinicId), cancellationToken);
        await context.Appointments.Where(a => a.DoctorId == demo.Id && a.ClinicId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClinicId, demoClinicId), cancellationToken);
    }

    /// <summary>
    /// Keeps the public demo bookable: adds working days (9:00–16:00) for every demo doctor, at their clinic,
    /// up to <see cref="ScheduleHorizonDays"/> ahead. Existing schedules and visits are never changed.
    /// </summary>
    public async Task<int> TopUpScheduleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return 0;
        var doctors = await context.Doctors.Where(d => d.LicenseNumber.StartsWith("DEMO-"))
            .Select(d => new { d.Id, ClinicId = d.DoctorClinics.OrderBy(l => l.ClinicId).Select(l => (int?)l.ClinicId).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var from = clock.Today.AddDays(1);
        var to = clock.Today.AddDays(ScheduleHorizonDays);
        var added = 0;
        foreach (var doctor in doctors)
        {
            var covered = (await context.DoctorSchedules.Where(s => s.DoctorId == doctor.Id && s.StartTime >= from && s.StartTime < to.AddDays(1))
                .Select(s => s.StartTime).ToListAsync(cancellationToken)).Select(s => s.Date).ToHashSet();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || covered.Contains(day)) continue;
                context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctor.Id, ClinicId = doctor.ClinicId, StartTime = day.AddHours(9), EndTime = day.AddHours(16), IsAvailable = true
                });
                added++;
            }
        }
        if (added > 0) await context.SaveChangesAsync(cancellationToken);
        return added;
    }

    public const string DemoDoctorLicense = "DEMO-001";
    public const int ScheduleHorizonDays = 28;
}

public sealed class DemoDataOptions
{
    public bool Enabled { get; set; }
    public string PatientPassword { get; set; } = string.Empty;
    public string StaffPassword { get; set; } = string.Empty;
}

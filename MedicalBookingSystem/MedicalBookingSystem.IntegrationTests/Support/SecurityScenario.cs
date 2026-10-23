using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Support;

internal sealed class SecurityScenario : IDisposable
{
    public const string Password = "Original2026!";
    private static readonly string PatientHash = new PasswordHasher<Patient>().HashPassword(new Patient(), Password);
    private static readonly string StaffHash = new PasswordHasher<StaffAccount>().HashPassword(new StaffAccount(), Password);
    public MedicalApiFactory Factory { get; }
    private SecurityScenario(MedicalApiFactory factory) => Factory = factory;
    public int PatientId { get; private set; }
    public int OtherPatientId { get; private set; }
    public int DoctorId { get; private set; }
    public int OtherDoctorId { get; private set; }
    public int AppointmentId { get; private set; }
    public int OtherAppointmentId { get; private set; }
    public int ClinicalAppointmentId { get; private set; }
    public int NoteId { get; private set; }
    public int PrescriptionId { get; private set; }
    public int ReviewId { get; private set; }
    public DateTime BookingDate { get; private set; }

    public static async Task<SecurityScenario> CreateAsync(MedicalApiFactory? factory = null)
    {
        var scenario = new SecurityScenario(factory ?? new MedicalApiFactory());
        await scenario.Factory.InitializeDatabaseAsync();
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ClinicClock>();
        var patient = new Patient { Email = "one@example.test", FirstName = "First", LastName = "Patient", PasswordHash = PatientHash, DateOfBirth = new(1990, 1, 1) };
        var otherPatient = new Patient { Email = "two@example.test", FirstName = "Second", LastName = "Patient", PasswordHash = PatientHash, DateOfBirth = new(1990, 1, 1) };
        var doctor = new Doctor { FirstName = "First", LastName = "Doctor", LicenseNumber = "TEST-1" };
        var otherDoctor = new Doctor { FirstName = "Second", LastName = "Doctor", LicenseNumber = "TEST-2" };
        db.AddRange(patient, otherPatient, doctor, otherDoctor);
        await db.SaveChangesAsync();
        db.StaffAccounts.AddRange(
            new StaffAccount { Email = "doctor@example.test", DisplayName = "First Doctor", DoctorId = doctor.Id, Role = StaffRole.Doctor, PasswordHash = StaffHash },
            new StaffAccount { Email = "other-doctor@example.test", DisplayName = "Second Doctor", DoctorId = otherDoctor.Id, Role = StaffRole.Doctor, PasswordHash = StaffHash },
            new StaffAccount { Email = "admin@example.test", DisplayName = "Admin", Role = StaffRole.Admin, PasswordHash = StaffHash },
            new StaffAccount { Email = "inactive@example.test", DisplayName = "Inactive", Role = StaffRole.Admin, IsActive = false, PasswordHash = StaffHash });
        scenario.BookingDate = clock.Today.AddDays(3).AddHours(10);
        db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = doctor.Id, StartTime = scenario.BookingDate, EndTime = scenario.BookingDate.AddHours(4), IsAvailable = true });
        var appointment = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, Date = scenario.BookingDate.AddDays(1) };
        var otherAppointment = new Appointment { PatientId = otherPatient.Id, DoctorId = otherDoctor.Id, Date = scenario.BookingDate.AddDays(1) };
        var completed = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, Date = clock.Today.AddDays(-2).AddHours(10), Status = AppointmentStatus.Completed };
        var clinical = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, Date = clock.Today.AddDays(-3).AddHours(10), Status = AppointmentStatus.Completed };
        var note = new Note { Appointment = completed, Content = "Confidential clinical note", CreatedDate = clock.UtcNow };
        var prescription = new Prescription { Appointment = completed, Medication = "Confidential medication", Dosage = "1 daily", CreatedDate = clock.UtcNow, ExpiryDate = clock.Today.AddDays(7) };
        var review = new Review { PatientId = otherPatient.Id, DoctorId = otherDoctor.Id, Rating = 5, Comment = "Public review" };
        db.AddRange(appointment, otherAppointment, completed, clinical, note, prescription, review);
        await db.SaveChangesAsync();
        scenario.PatientId = patient.Id;
        scenario.OtherPatientId = otherPatient.Id;
        scenario.DoctorId = doctor.Id;
        scenario.OtherDoctorId = otherDoctor.Id;
        scenario.AppointmentId = appointment.Id;
        scenario.OtherAppointmentId = otherAppointment.Id;
        scenario.NoteId = note.Id;
        scenario.ClinicalAppointmentId = clinical.Id;
        scenario.PrescriptionId = prescription.Id;
        scenario.ReviewId = review.Id;
        return scenario;
    }

    public async Task<HttpClient> SignInAsync(string email = "one@example.test", bool staff = false)
    {
        var client = Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(staff ? "/api/auth/staff/login" : "/api/auth/login",
            new LoginDto { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());
        return client;
    }

    public void Dispose() => Factory.Dispose();
}

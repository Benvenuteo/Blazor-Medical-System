using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("GET", "/api/patients/profile")]
    [InlineData("PUT", "/api/patients/profile")]
    [InlineData("GET", "/api/appointments/upcoming")]
    [InlineData("GET", "/api/appointments/history")]
    [InlineData("GET", "/api/appointments/1")]
    [InlineData("POST", "/api/appointments")]
    [InlineData("PUT", "/api/appointments/1")]
    [InlineData("DELETE", "/api/appointments/1")]
    [InlineData("POST", "/api/reviews")]
    [InlineData("DELETE", "/api/reviews/1")]
    [InlineData("GET", "/api/medical-records/patient/1")]
    [InlineData("GET", "/api/medical-records/notes/1")]
    [InlineData("GET", "/api/medical-records/prescriptions/1")]
    [InlineData("POST", "/api/medical-records/notes")]
    [InlineData("POST", "/api/medical-records/prescriptions")]
    public async Task PrivateEndpoint_WithoutToken_Returns401(string method, string path)
    {
        using var factory = new MedicalApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_IgnoresSpoofedPatientId_AndUpdatesOnlyAuthenticatedPatient()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var profile = await client.GetAsync($"/api/patients/profile?patientId={scenario.OtherPatientId}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var body = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.Equal(scenario.PatientId, body.RootElement.GetProperty("id").GetInt32());
        using var update = await client.PutAsJsonAsync($"/api/patients/profile?patientId={scenario.OtherPatientId}", new { firstName = "Updated", lastName = "Patient" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Updated", (await db.Patients.SingleAsync(p => p.Id == scenario.PatientId)).FirstName);
        Assert.Equal("Second", (await db.Patients.SingleAsync(p => p.Id == scenario.OtherPatientId)).FirstName);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ForeignAppointment_CannotBeReadRescheduledOrCancelled(string method)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/appointments/{scenario.OtherAppointmentId}?patientId={scenario.OtherPatientId}");
        if (method == "PUT") request.Content = JsonContent.Create(new { newDate = scenario.BookingDate.AddHours(1) });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var appointment = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Appointments.SingleAsync(a => a.Id == scenario.OtherAppointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(scenario.BookingDate.AddDays(1), appointment.Date);
    }

    [Fact]
    public async Task Booking_UsesPatientFromSignedToken_EvenWithSpoofedBodyId()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var response = await client.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate, patientId = scenario.OtherPatientId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(scenario.PatientId, body.RootElement.GetProperty("patientId").GetInt32());
        using var details = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
    }

    [Theory]
    [InlineData("one@example.test", false, HttpStatusCode.OK)]
    [InlineData("two@example.test", false, HttpStatusCode.Forbidden)]
    [InlineData("doctor@example.test", true, HttpStatusCode.OK)]
    [InlineData("other-doctor@example.test", true, HttpStatusCode.Forbidden)]
    [InlineData("admin@example.test", true, HttpStatusCode.Forbidden)]
    public async Task ClinicalDocuments_OnlyPatientOwnerAndAssignedDoctorCanRead(string email, bool staff, HttpStatusCode expected)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync(email, staff);
        foreach (var path in new[] { $"/api/medical-records/notes/{scenario.NoteId}", $"/api/medical-records/prescriptions/{scenario.PrescriptionId}" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.Forbidden)
                Assert.DoesNotContain("Confidential", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task ForeignPatientDocumentsAndReviewDeletion_AreDenied()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using var documents = await client.GetAsync($"/api/medical-records/patient/{scenario.OtherPatientId}");
        using var delete = await client.DeleteAsync($"/api/reviews/{scenario.ReviewId}?patientId={scenario.OtherPatientId}");
        Assert.Equal(HttpStatusCode.Forbidden, documents.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reviews.AnyAsync(r => r.Id == scenario.ReviewId));
    }

    [Theory]
    [InlineData("one@example.test", false)]
    [InlineData("admin@example.test", true)]
    [InlineData("other-doctor@example.test", true)]
    public async Task ClinicalWrites_RequireDoctorAssignedToAppointment(string email, bool staff)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync(email, staff);
        using var note = await client.PostAsJsonAsync($"/api/medical-records/notes?doctorId={scenario.DoctorId}", new { appointmentId = scenario.AppointmentId, content = "New confidential clinical note" });
        using var prescription = await client.PostAsJsonAsync("/api/medical-records/prescriptions", new { appointmentId = scenario.AppointmentId, medication = "Test", dosage = "Once daily", validityDays = 7 });
        Assert.Equal(HttpStatusCode.Forbidden, note.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, prescription.StatusCode);
        await using var scope = scenario.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Notes.AnyAsync(n => n.AppointmentId == scenario.AppointmentId));
        Assert.False(await db.Prescriptions.AnyAsync(n => n.AppointmentId == scenario.AppointmentId));
    }

    [Fact]
    public async Task AssignedDoctor_CanCreateDocument_ButCannotActAsPatient()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync("doctor@example.test", true);
        using var note = await client.PostAsJsonAsync("/api/medical-records/notes", new { appointmentId = scenario.AppointmentId, content = "New confidential clinical note" });
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        using var read = await client.GetAsync(note.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var patientData = await client.GetAsync($"/api/patients/profile?patientId={scenario.PatientId}");
        Assert.Equal(HttpStatusCode.Forbidden, patientData.StatusCode);
    }

    [Theory]
    [InlineData("inactive@example.test", SecurityScenario.Password)]
    [InlineData("doctor@example.test", "Incorrect2026!")]
    public async Task StaffLogin_InactiveAccountOrWrongPassword_Returns401(string email, string password)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/staff/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_CannotChoosePrivilegedRole()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "new@example.test", password = SecurityScenario.Password, firstName = "Jan", lastName = "Testowy",
            phoneNumber = "+48500000000", dateOfBirth = new DateTime(1990, 1, 1), role = Roles.Admin, doctorId = 1
        });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var body = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;
        Assert.Equal(Roles.Patient, claims.Single(c => c.Type == ClaimTypes.Role).Value);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var clinicalWrite = await client.PostAsJsonAsync("/api/medical-records/notes", new { appointmentId = 1, content = "Clinical note" });
        Assert.Equal(HttpStatusCode.Forbidden, clinicalWrite.StatusCode);
    }

    [Fact]
    public async Task ExcessiveAuthenticationAttempts_Return429_WithoutBlockingHealthChecks()
    {
        using var factory = new MedicalApiFactory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/staff/login", new { email = "invalid", password = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task InvalidJwt_IsRejectedByRealBearerHandler(string invalidPart)
    {
        using var factory = new MedicalApiFactory();
        using var client = factory.CreateClient();
        var secret = invalidPart == "signature" ? "wrong-signing-key-at-least-thirty-two-bytes" : "integration-tests-only-signing-key-at-least-thirty-two-bytes";
        var expires = DateTimeOffset.UtcNow.AddMinutes(invalidPart == "expired" ? -1 : 5);
        var principal = AccountClaims.Create(1, "Test", "test@example.test", Roles.Patient, expires);
        var jwt = new JwtSecurityToken(invalidPart == "issuer" ? "wrong" : "MedicalBookingSystem",
            invalidPart == "audience" ? "wrong" : "MedicalBookingSystem.Clients", principal.Claims,
            DateTime.UtcNow.AddMinutes(-5), expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        using var response = await client.GetAsync("/api/patients/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

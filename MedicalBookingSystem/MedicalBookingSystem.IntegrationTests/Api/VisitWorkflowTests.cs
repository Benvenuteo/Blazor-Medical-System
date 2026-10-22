using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class VisitWorkflowTests
{
    [Fact]
    public async Task PatientBooks_DoctorCompletes_DocumentsAndReviewBecomeAvailable_ThroughRealHttp()
    {
        var time = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(testClock: time));
        using var patient = scenario.Factory.CreateClient();
        using var registration = await patient.PostAsJsonAsync("/api/auth/register", new
        {
            email = "workflow@example.test", password = SecurityScenario.Password, firstName = "Jan", lastName = "Testowy",
            phoneNumber = "+48500000000", dateOfBirth = new DateTime(1990, 1, 1)
        });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var registrationBody = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        patient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationBody.RootElement.GetProperty("token").GetString());
        using var booking = await patient.PostAsJsonAsync("/api/appointments", new { doctorId = scenario.DoctorId, date = scenario.BookingDate });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        using var bookingBody = JsonDocument.Parse(await booking.Content.ReadAsStringAsync());
        var id = bookingBody.RootElement.GetProperty("id").GetInt32();
        using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        var note = new { appointmentId = id, content = "Synthetic visit documentation." };
        var prescription = new { appointmentId = id, medication = "Synthetic medication", dosage = "Once daily", validityDays = 7 };
        var review = new { doctorId = scenario.DoctorId, rating = 5, comment = "Synthetic review" };
        using var earlyCompletion = await doctor.PostAsync($"/api/doctor/appointments/{id}/complete", null);
        using var earlyNote = await doctor.PostAsJsonAsync("/api/medical-records/notes", note);
        using var earlyReview = await patient.PostAsJsonAsync("/api/reviews", review);
        Assert.Equal(HttpStatusCode.Conflict, earlyCompletion.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, earlyNote.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, earlyReview.StatusCode);
        using var patientCompletion = await patient.PostAsync($"/api/doctor/appointments/{id}/complete", null);
        Assert.Equal(HttpStatusCode.Forbidden, patientCompletion.StatusCode);
        using var wrongDoctor = await scenario.SignInAsync("other-doctor@example.test", true);
        using var foreignCompletion = await wrongDoctor.PostAsync($"/api/doctor/appointments/{id}/complete", null);
        Assert.Equal(HttpStatusCode.Forbidden, foreignCompletion.StatusCode);

        // Advance the clinic and token clocks to the exact end boundary; no database date/status edits.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        time.SetUtcNow(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(scenario.BookingDate.AddMinutes(30), zone)));
        using var renewedDoctor = await scenario.SignInAsync("doctor@example.test", true);
        using var renewedPatient = await scenario.SignInAsync("workflow@example.test");
        using var completed = await renewedDoctor.PostAsync($"/api/doctor/appointments/{id}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var completedTwice = await renewedDoctor.PostAsync($"/api/doctor/appointments/{id}/complete", null);
        using var cancellation = await renewedPatient.DeleteAsync($"/api/appointments/{id}");
        using var reschedule = await renewedPatient.PutAsJsonAsync($"/api/appointments/{id}", new { newDate = scenario.BookingDate.AddDays(2) });
        Assert.Equal(HttpStatusCode.Conflict, completedTwice.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancellation.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reschedule.StatusCode);
        using var noteCreated = await renewedDoctor.PostAsJsonAsync("/api/medical-records/notes", note);
        using var prescriptionCreated = await renewedDoctor.PostAsJsonAsync("/api/medical-records/prescriptions", prescription);
        using var reviewCreated = await renewedPatient.PostAsJsonAsync("/api/reviews", review);
        Assert.Equal(HttpStatusCode.Created, noteCreated.StatusCode);
        Assert.Equal(HttpStatusCode.Created, prescriptionCreated.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reviewCreated.StatusCode);
        using var duplicateNote = await renewedDoctor.PostAsJsonAsync("/api/medical-records/notes", note);
        using var duplicatePrescription = await renewedDoctor.PostAsJsonAsync("/api/medical-records/prescriptions", prescription);
        using var duplicateReview = await renewedPatient.PostAsJsonAsync("/api/reviews", review);
        Assert.Equal(HttpStatusCode.Conflict, duplicateNote.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicatePrescription.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateReview.StatusCode);
        var patientNote = await renewedPatient.GetFromJsonAsync<NoteDto>(noteCreated.Headers.Location);
        var patientPrescription = await renewedPatient.GetFromJsonAsync<PrescriptionDto>(prescriptionCreated.Headers.Location);
        Assert.Equal(note.content, patientNote!.Content);
        Assert.Equal(prescription.medication, patientPrescription!.Medication);
        var history = await renewedPatient.GetFromJsonAsync<MedicalBookingSystem.SharedKernel.PagedResult<AppointmentDto>>("/api/appointments/history");
        var visit = Assert.Single(history!.Items);
        Assert.Equal(id, visit.Id);
        Assert.Equal("Completed", visit.Status);
        Assert.True(visit.HasNote);
        Assert.True(visit.HasPrescription);
    }
}

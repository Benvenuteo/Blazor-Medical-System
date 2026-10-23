using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class WorklistTests
{
    [Fact]
    public async Task PatientAndDoctorHistory_UseStableNonOverlappingPages_WithoutClinicalText()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using (var scope = scenario.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Appointments.AddRange(Enumerable.Range(10, 24).Select(i => new Appointment { PatientId = scenario.PatientId, DoctorId = scenario.DoctorId, Status = AppointmentStatus.Completed, Date = scenario.BookingDate.AddDays(-i) }));
            await db.SaveChangesAsync();
        }
        using var patient = await scenario.SignInAsync(); using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        foreach (var (client, path) in new[] { (patient, "/api/appointments/history"), (doctor, "/api/doctor/appointments/completed") })
        {
            var first = (await client.GetFromJsonAsync<PagedResult<AppointmentDto>>(path + "?page=1&pageSize=10"))!;
            var second = (await client.GetFromJsonAsync<PagedResult<AppointmentDto>>(path + "?page=2&pageSize=10"))!;
            Assert.Equal(26, first.TotalCount); Assert.Equal(10, first.Items.Count); Assert.Equal(10, second.Items.Count);
            Assert.Empty(first.Items.Select(a => a.Id).Intersect(second.Items.Select(a => a.Id)));
            Assert.All(first.Items.Concat(second.Items), a => Assert.Equal(scenario.PatientId, a.PatientId));
            var beyond = (await client.GetFromJsonAsync<PagedResult<AppointmentDto>>(path + "?page=100"))!; Assert.Empty(beyond.Items); Assert.Equal(26, beyond.TotalCount);
        }
    }

    [Fact]
    public async Task MissingDocumentFilter_IsAppliedBeforePaging_AndDateRangeIsInclusive()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        var page = (await doctor.GetFromJsonAsync<PagedResult<AppointmentDto>>("/api/doctor/appointments/completed?missingDocument=Note&pageSize=1"))!;
        Assert.Equal(1, page.TotalCount); Assert.Equal(scenario.ClinicalAppointmentId, Assert.Single(page.Items).Id);
        var date = page.Items[0].Date.ToString("s");
        var range = (await doctor.GetFromJsonAsync<PagedResult<AppointmentDto>>($"/api/doctor/appointments/completed?from={date}&to={date}"))!;
        Assert.Equal(page.Items[0].Id, Assert.Single(range.Items).Id);
    }

    [Fact]
    public async Task DocumentDirectory_PagesAcrossBothDocumentTypes_WithoutReleasingContent()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var patient = await scenario.SignInAsync();
        var path = $"/api/medical-records/patient/{scenario.PatientId}";
        using var firstResponse = await patient.GetAsync(path + "?page=1&pageSize=1");
        var first = (await firstResponse.Content.ReadFromJsonAsync<PagedResult<MedicalDocumentSummaryDto>>())!;
        var second = (await patient.GetFromJsonAsync<PagedResult<MedicalDocumentSummaryDto>>(path + "?page=2&pageSize=1"))!;
        Assert.Equal(2, first.TotalCount); Assert.Single(first.Items); Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].DocumentType, second.Items[0].DocumentType);
        var body = await firstResponse.Content.ReadAsStringAsync(); Assert.DoesNotContain("Confidential", body); Assert.DoesNotContain("content", body); Assert.DoesNotContain("medication", body);
    }

    [Theory]
    [InlineData("PatientId")]
    [InlineData("DoctorId")]
    public async Task HistoryQuery_UsesOwnerStatusDateIndex(string ownerColumn)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        using var command = connection.CreateCommand();
        // The column comes only from the two fixed theory cases, never from a request.
        command.CommandText = $"EXPLAIN QUERY PLAN SELECT Id FROM Appointments WHERE {ownerColumn}=1 AND Status='Completed' ORDER BY Date DESC, Id DESC LIMIT 10";
        using var reader = await command.ExecuteReaderAsync(); var plan = new List<string>();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.Contains(plan, row => row.Contains($"IX_Appointments_{ownerColumn}_Status_Date_Id", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/appointments/history?pageSize=51", "one@example.test", false)]
    [InlineData("/api/appointments/upcoming?page=0", "one@example.test", false)]
    [InlineData("/api/doctor/appointments?from=2026-11-02&to=2026-11-01", "doctor@example.test", true)]
    [InlineData("/api/doctor/appointments/completed?missingDocument=unknown", "doctor@example.test", true)]
    [InlineData("/api/reviews/doctor/1?page=0", "one@example.test", false)]
    [InlineData("/api/admin/clinical-audit?pageSize=10000", "admin@example.test", true)]
    public async Task Worklists_RejectInvalidLimitsAndFilters(string path, string email, bool staff)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var client = await scenario.SignInAsync(email, staff);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }
}

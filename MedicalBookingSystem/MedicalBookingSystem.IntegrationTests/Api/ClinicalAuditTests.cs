using System.Net;
using System.Net.Http.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class ClinicalAuditTests
{
    [Fact]
    public async Task ClinicalReads_RecordActorAndResource_WithoutCopyingClinicalContent()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var patient = await scenario.SignInAsync();
        foreach (var path in new[] { $"/api/medical-records/notes/{scenario.NoteId}", $"/api/medical-records/prescriptions/{scenario.PrescriptionId}", $"/api/appointments/{scenario.AppointmentId}", $"/api/medical-records/patient/{scenario.PatientId}" })
            Assert.Equal(HttpStatusCode.OK, (await patient.GetAsync(path)).StatusCode);
        using var admin = await scenario.SignInAsync("admin@example.test", true);
        using var response = await admin.GetAsync("/api/admin/clinical-audit?pageSize=2");
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<ClinicalAuditDto>>())!;
        Assert.Equal(4, page.TotalCount); Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, e => { Assert.Equal($"patient:{scenario.PatientId}", e.Actor); Assert.Equal("Patient", e.ActorRole); Assert.Equal("Read", e.Action); Assert.Equal(DateTimeKind.Utc, e.OccurredAtUtc.Kind); });
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Confidential", json); Assert.DoesNotContain("Password", json);
    }

    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("one@example.test", false, HttpStatusCode.Forbidden)]
    [InlineData("doctor@example.test", true, HttpStatusCode.Forbidden)]
    public async Task AuditDirectory_RequiresAdministrator(string? email, bool staff, HttpStatusCode expected)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var client = email is null ? scenario.Factory.CreateClient() : await scenario.SignInAsync(email, staff);
        Assert.Equal(expected, (await client.GetAsync("/api/admin/clinical-audit")).StatusCode);
    }

    [Fact]
    public async Task DeniedRead_DoesNotCreateSuccessfulAccessRecord()
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var foreign = await scenario.SignInAsync("two@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await foreign.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var scope = scenario.Factory.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ClinicalAudit.ToListAsync());
    }

    [Fact]
    public async Task UnavailableAuditStorage_WithholdsClinicalRead()
    {
        var rejection = new RejectAuditSave();
        using var scenario = await SecurityScenario.CreateAsync(new MedicalApiFactory(interceptor: rejection));
        using var patient = await scenario.SignInAsync(); rejection.Active = true;
        using var response = await patient.GetAsync($"/api/medical-records/notes/{scenario.NoteId}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Confidential", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RejectedAuditInsert_RollsBackClinicalDocumentInSameTransaction()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using (var scope = scenario.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectAudit BEFORE INSERT ON ClinicalAudit BEGIN SELECT RAISE(ABORT, 'simulated_audit_failure'); END;");
        using var doctor = await scenario.SignInAsync("doctor@example.test", true);
        using var response = await doctor.PostAsJsonAsync("/api/medical-records/notes", new { appointmentId = scenario.ClinicalAppointmentId, content = "Must roll back" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var check = scenario.Factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Notes.AnyAsync(n => n.AppointmentId == scenario.ClinicalAppointmentId)); Assert.Empty(await db.ClinicalAudit.ToListAsync());
    }

    [Theory]
    [InlineData("UPDATE ClinicalAudit SET Actor = 'changed'")]
    [InlineData("DELETE FROM ClinicalAudit")]
    public async Task AuditRecords_CannotBeChangedOrDeletedThroughOrdinaryDatabaseWrites(string sql)
    {
        using var scenario = await SecurityScenario.CreateAsync(); using var patient = await scenario.SignInAsync();
        Assert.Equal(HttpStatusCode.OK, (await patient.GetAsync($"/api/medical-records/notes/{scenario.NoteId}")).StatusCode);
        using var scope = scenario.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var error = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Contains("clinical_audit_immutable", error.Message); Assert.Single(await db.ClinicalAudit.ToListAsync());
    }
    private sealed class RejectAuditSave : SaveChangesInterceptor
    {
        public bool Active { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Active && data.Context!.ChangeTracker.Entries<ClinicalAuditEntry>().Any(e => e.State == EntityState.Added)) throw new IOException("Simulated unavailable audit storage");
            return ValueTask.FromResult(result);
        }
    }
}

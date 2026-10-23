using System.Text.Json;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.Infrastructure.Maintenance;
using MedicalBookingSystem.IntegrationTests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class BackupRecoveryTests
{
    [Fact]
    public async Task OnlineBackup_IncludesCommittedWalDataAndReferencedMedia_AndRestoreRevokesSessions()
    {
        using var fixture = await Fixture.CreateAsync();
        using var writer = new SqliteConnection(fixture.ConnectionString); await writer.OpenAsync();
        using var command = writer.CreateCommand(); command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE Patients SET FirstName='Committed in WAL';"; await command.ExecuteNonQueryAsync();
        Assert.True(File.Exists(fixture.DatabasePath + "-wal"));
        var service = new BackupService(); var manifest = await service.CreateAsync(fixture.DatabasePath, fixture.Media, fixture.Backup);
        Assert.Equal(2, manifest.Files.Count); Assert.All(manifest.Files, f => Assert.Equal(64, f.Sha256.Length));
        await service.RestoreAsync(fixture.Backup, fixture.Restored);
        await using var restored = Context(Path.Combine(fixture.Restored, "medical.db"));
        Assert.Equal("Committed in WAL", (await restored.Patients.FirstAsync()).FirstName);
        Assert.Equal(fixture.NoteCount, await restored.Notes.CountAsync());
        Assert.Equal(fixture.AuditCount, await restored.ClinicalAudit.CountAsync());
        Assert.All(await restored.Patients.ToListAsync(), p => Assert.DoesNotContain(p.SecurityStamp, fixture.SecurityStamps));
        Assert.All(await restored.StaffAccounts.ToListAsync(), a => Assert.DoesNotContain(a.SecurityStamp, fixture.SecurityStamps));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(fixture.Media, fixture.Photo)), await File.ReadAllBytesAsync(Path.Combine(fixture.Restored, "photos", fixture.Photo)));
        Assert.True(File.Exists(Path.Combine(fixture.Restored, "recovery.json")));
        Assert.DoesNotContain("-wal", string.Join(',', Directory.GetFiles(fixture.Backup)));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("photo")]
    [InlineData("missing-photo")]
    [InlineData("path-traversal")]
    [InlineData("unlisted-file")]
    public async Task DamagedOrUnexpectedBackup_IsRejectedBeforeCreatingRestoreDestination(string damage)
    {
        using var fixture = await Fixture.CreateAsync(); var service = new BackupService();
        var manifest = await service.CreateAsync(fixture.DatabasePath, fixture.Media, fixture.Backup);
        switch (damage)
        {
            case "database": await File.AppendAllTextAsync(Path.Combine(fixture.Backup, "medical.db"), "damaged"); break;
            case "photo": await File.AppendAllTextAsync(Path.Combine(fixture.Backup, "photos", fixture.Photo), "damaged"); break;
            case "missing-photo": File.Delete(Path.Combine(fixture.Backup, "photos", fixture.Photo)); break;
            case "path-traversal": await File.WriteAllTextAsync(Path.Combine(fixture.Backup, "manifest.json"), JsonSerializer.Serialize(manifest with { Files = [new BackupFile("../escape", 1, new string('A', 64))] })); break;
            case "unlisted-file": await File.WriteAllTextAsync(Path.Combine(fixture.Backup, "unlisted.txt"), "unexpected"); break;
        }
        var error = await Record.ExceptionAsync(() => service.RestoreAsync(fixture.Backup, fixture.Restored));
        Assert.True(error is IOException or InvalidDataException, error?.ToString());
        Assert.False(Directory.Exists(fixture.Restored));
    }

    [Fact]
    public async Task MissingReferencedPhoto_AbortsBackupPublication_AndCleansStagingDirectory()
    {
        using var fixture = await Fixture.CreateAsync(); File.Delete(Path.Combine(fixture.Media, fixture.Photo));
        await Assert.ThrowsAnyAsync<IOException>(() => new BackupService().CreateAsync(fixture.DatabasePath, fixture.Media, fixture.Backup));
        Assert.False(Directory.Exists(fixture.Backup)); Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(fixture.DatabasePath)!, "*.pending-*"));
    }

    [Fact]
    public async Task ExistingDestination_IsNeverOverwritten()
    {
        using var fixture = await Fixture.CreateAsync(); var service = new BackupService();
        await service.CreateAsync(fixture.DatabasePath, fixture.Media, fixture.Backup);
        Directory.CreateDirectory(fixture.Restored); var marker = Path.Combine(fixture.Restored, "keep.txt"); await File.WriteAllTextAsync(marker, "keep");
        await Assert.ThrowsAsync<IOException>(() => service.RestoreAsync(fixture.Backup, fixture.Restored));
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(fixture.DatabasePath, fixture.Media, fixture.Backup));
        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
    }

    private static ApplicationDbContext Context(string path) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()).Options);
    private sealed class Fixture(TemporarySqliteDatabase file, SecurityScenario scenario) : IDisposable
    {
        private SecurityScenario Scenario { get; } = scenario;
        public string ConnectionString => file.ConnectionString;
        public string DatabasePath => new SqliteConnectionStringBuilder(ConnectionString).DataSource;
        private string Root => Path.GetDirectoryName(DatabasePath)!;
        public string Media => Path.Combine(Root, "photos");
        public string Backup => Path.Combine(Root, "backup");
        public string Restored => Path.Combine(Root, "restored");
        public string Photo { get; } = Guid.NewGuid().ToString("N") + ".png";
        public List<Guid> SecurityStamps { get; private set; } = [];
        public int NoteCount { get; private set; }
        public int AuditCount { get; private set; }
        public static async Task<Fixture> CreateAsync()
        {
            var file = new TemporarySqliteDatabase();
            var fixture = new Fixture(file, await SecurityScenario.CreateAsync(new MedicalApiFactory(file.ConnectionString)));
            Directory.CreateDirectory(fixture.Media);
            await File.WriteAllBytesAsync(Path.Combine(fixture.Media, fixture.Photo), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            using var scope = fixture.Scenario.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Doctors.FirstAsync()).ImageUrl = "https://media.example.test/media/doctors/" + fixture.Photo;
            db.ClinicalAudit.Add(new ClinicalAuditEntry { Actor = "patient:1", ActorRole = "Patient", Action = "Read", Resource = "Note", ResourceId = 1, PatientId = 1, OccurredAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            fixture.SecurityStamps = (await db.Patients.Select(p => p.SecurityStamp).ToListAsync()).Concat(await db.StaffAccounts.Select(a => a.SecurityStamp).ToListAsync()).ToList();
            fixture.NoteCount = await db.Notes.CountAsync(); fixture.AuditCount = await db.ClinicalAudit.CountAsync(); return fixture;
        }
        public void Dispose() { Scenario.Dispose(); file.Dispose(); }
    }
}

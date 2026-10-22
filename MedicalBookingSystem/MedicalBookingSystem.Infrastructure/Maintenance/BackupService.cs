using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace MedicalBookingSystem.Infrastructure.Maintenance;

public sealed record BackupFile(string Path, long Bytes, string Sha256);
public sealed record BackupManifest(int FormatVersion, DateTime CreatedAtUtc, IReadOnlyList<BackupFile> Files);

/// <summary>SQLite online snapshots with referenced media, verified before atomic publication to a new directory.</summary>
public sealed partial class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    [GeneratedRegex("^[a-f0-9]{32}\\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex PhotoName();

    public async Task<BackupManifest> CreateAsync(string databasePath, string mediaDirectory, string destination, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Source database does not exist.", databasePath);
        var staging = PrepareDestination(destination);
        try
        {
            var snapshot = Path.Combine(staging, "medical.db");
            using (var source = Connection(databasePath, SqliteOpenMode.ReadOnly))
            using (var target = Connection(snapshot, SqliteOpenMode.ReadWriteCreate))
            {
                await source.OpenAsync(cancellationToken); await target.OpenAsync(cancellationToken);
                // Copying the live .db file alone would lose committed WAL pages.
                source.BackupDatabase(target);
                using var journal = target.CreateCommand();
                journal.CommandText = "PRAGMA journal_mode=DELETE";
                journal.ExecuteScalar();
            }
            var photos = await ReferencedPhotosAsync(snapshot, cancellationToken);
            if (photos.Count > 0) Directory.CreateDirectory(Path.Combine(staging, "photos"));
            foreach (var photo in photos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = Path.Combine(mediaDirectory, photo);
                RejectLink(source);
                File.Copy(source, Path.Combine(staging, "photos", photo));
            }
            var files = new List<BackupFile> { await DescribeAsync(staging, "medical.db", cancellationToken) };
            foreach (var photo in photos.Order(StringComparer.Ordinal))
                files.Add(await DescribeAsync(staging, "photos/" + photo, cancellationToken));
            var manifest = new BackupManifest(1, DateTime.UtcNow, files);
            await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
            await VerifyAsync(staging, cancellationToken);
            Directory.Move(staging, Path.GetFullPath(destination));
            return manifest;
        }
        catch { Directory.Delete(staging, true); throw; }
    }

    public async Task<BackupManifest> VerifyAsync(string directory, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(directory); RejectLink(root);
        var manifestPath = Path.Combine(root, "manifest.json"); RejectLink(manifestPath);
        if (new FileInfo(manifestPath).Length > 1024 * 1024) throw new InvalidDataException("Backup manifest exceeds the supported size.");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken))
            ?? throw new InvalidDataException("Missing backup manifest.");
        if (manifest.FormatVersion != 1 || manifest.Files is null || manifest.Files.Count == 0) throw new InvalidDataException("Unsupported or empty backup manifest.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || file.Bytes < 0 || file.Sha256 is null || file.Sha256.Length != 64)
                throw new InvalidDataException("Invalid file metadata in backup manifest.");
            if (file.Path != "medical.db" && !(file.Path.StartsWith("photos/", StringComparison.Ordinal) && PhotoName().IsMatch(file.Path[7..])))
                throw new InvalidDataException("Unsupported path in backup manifest.");
            if (!names.Add(file.Path)) throw new InvalidDataException("Duplicate backup path.");
            var actual = await DescribeAsync(root, file.Path, cancellationToken);
            if (actual.Bytes != file.Bytes || !string.Equals(actual.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup content does not match its checksum: " + file.Path);
        }
        if (!names.Contains("medical.db")) throw new InvalidDataException("Database is missing from the manifest.");
        var actualNames = Directory.EnumerateFiles(root).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actualNames.SetEquals(new[] { "medical.db", "manifest.json" })) throw new InvalidDataException("Unexpected files in backup root.");
        var subdirectories = Directory.GetDirectories(root);
        if (subdirectories.Any(p => Path.GetFileName(p) != "photos")) throw new InvalidDataException("Unexpected backup directory.");
        var photoDirectory = Path.Combine(root, "photos");
        if (Directory.Exists(photoDirectory))
        {
            RejectLink(photoDirectory);
            if (Directory.GetDirectories(photoDirectory).Length != 0) throw new InvalidDataException("Nested photo directories are not supported.");
            var actualPhotos = Directory.EnumerateFiles(photoDirectory).Select(p => "photos/" + Path.GetFileName(p));
            if (!names.Where(p => p != "medical.db").ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(actualPhotos))
                throw new InvalidDataException("Photo files differ from the manifest.");
        }
        var referenced = await ReferencedPhotosAsync(Path.Combine(root, "medical.db"), cancellationToken);
        if (!referenced.Select(p => "photos/" + p).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(names.Where(p => p != "medical.db")))
            throw new InvalidDataException("Database photo references differ from backup content.");
        return manifest;
    }

    /// <summary>Restore to a fresh directory; never replace a running database or reactivate pre-backup sessions.</summary>
    public async Task RestoreAsync(string backupDirectory, string destination, CancellationToken cancellationToken = default)
    {
        var manifest = await VerifyAsync(backupDirectory, cancellationToken);
        var staging = PrepareDestination(destination);
        try
        {
            foreach (var file in manifest.Files)
            {
                var target = Path.Combine(staging, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(backupDirectory, file.Path), target);
            }
            File.Copy(Path.Combine(backupDirectory, "manifest.json"), Path.Combine(staging, "manifest.json"));
            // Verify the copied bytes too, before changing any restored data.
            await VerifyAsync(staging, cancellationToken);
            using (var db = Connection(Path.Combine(staging, "medical.db"), SqliteOpenMode.ReadWrite))
            {
                await db.OpenAsync(cancellationToken);
                using var transaction = db.BeginTransaction();
                foreach (var table in new[] { "Patients", "StaffAccounts" })
                {
                    using var command = db.CreateCommand(); command.Transaction = transaction;
                    command.CommandText = $"UPDATE {table} SET SecurityStamp = {RandomGuidSql}, Version = {RandomGuidSql}";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                transaction.Commit();
            }
            File.Delete(Path.Combine(staging, "manifest.json"));
            await File.WriteAllTextAsync(Path.Combine(staging, "recovery.json"), JsonSerializer.Serialize(new
                { SourceCreatedAtUtc = manifest.CreatedAtUtc, RestoredAtUtc = DateTime.UtcNow, SessionsRevoked = true }, JsonOptions), cancellationToken);
            Directory.Move(staging, Path.GetFullPath(destination));
        }
        catch { Directory.Delete(staging, true); throw; }
    }

    private const string RandomGuidSql = "upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)))";
    private static SqliteConnection Connection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
        { DataSource = Path.GetFullPath(path), Mode = mode, Pooling = false, ForeignKeys = true }.ToString());
    private static string PrepareDestination(string destination)
    {
        var full = Path.GetFullPath(destination);
        if (Directory.Exists(full) || File.Exists(full)) throw new IOException("Destination already exists; choose a new directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var staging = full + ".pending-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return staging;
    }
    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links are not supported in backups.");
    }
    private static async Task<BackupFile> DescribeAsync(string root, string relative, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, relative);
        if (relative.StartsWith("photos/", StringComparison.Ordinal)) RejectLink(Path.Combine(root, "photos"));
        RejectLink(path);
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return new(relative, stream.Length, Convert.ToHexString(hash));
    }
    private static async Task<HashSet<string>> ReferencedPhotosAsync(string database, CancellationToken cancellationToken)
    {
        using var db = Connection(database, SqliteOpenMode.ReadOnly); await db.OpenAsync(cancellationToken);
        using var check = db.CreateCommand(); check.CommandText = "PRAGMA quick_check";
        if ((string?)await check.ExecuteScalarAsync(cancellationToken) != "ok") throw new InvalidDataException("SQLite integrity check failed.");
        check.CommandText = "PRAGMA foreign_key_check";
        using (var reader = await check.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) throw new InvalidDataException("SQLite foreign key check failed.");
        check.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AccountSessions'";
        if ((long)(await check.ExecuteScalarAsync(cancellationToken))! != 1) throw new InvalidDataException("Unsupported database schema.");
        check.CommandText = "SELECT ImageUrl FROM Doctors";
        using var photos = await check.ExecuteReaderAsync(cancellationToken);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await photos.ReadAsync(cancellationToken))
        {
            var url = photos.GetString(0); const string marker = "/media/doctors/";
            var index = url.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) continue;
            var fileName = url[(index + marker.Length)..];
            if (!PhotoName().IsMatch(fileName)) throw new InvalidDataException("Unsupported owned photo reference.");
            names.Add(fileName);
        }
        return names;
    }
}

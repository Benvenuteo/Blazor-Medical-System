using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.Infrastructure.Maintenance;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

try
{
    var backups = new BackupService();
    switch (args)
    {
        case ["backup", var database, var media, var destination]:
            var manifest = await backups.CreateAsync(database, media, destination);
            Console.WriteLine($"Backup verified: {manifest.Files.Count} files in {Path.GetFullPath(destination)}");
            break;
        case ["verify", var directory]:
            var verified = await backups.VerifyAsync(directory);
            Console.WriteLine($"Backup verified: {verified.Files.Count} files, created {verified.CreatedAtUtc:O}");
            break;
        case ["restore", var bundle, var destination]:
            await backups.RestoreAsync(bundle, destination);
            Console.WriteLine($"Restored to {Path.GetFullPath(destination)}. All restored sessions are revoked.");
            break;
        case ["create-admin", var database, var email, var name]:
            await SetAdministratorAsync(database, email, name, create: true);
            break;
        case ["reset-admin", var database, var email]:
            await SetAdministratorAsync(database, email, null, create: false);
            break;
        default:
            Console.Error.WriteLine("Commands:\n  backup <database-file> <media-directory> <new-backup-directory>\n  verify <backup-directory>\n  restore <backup-directory> <new-data-directory>\n  create-admin <database-file> <email> <display-name>\n  reset-admin <database-file> <email>\nPasswords are read from the console or one redirected stdin line, never command arguments.");
            return 2;
    }
    return 0;
}
catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or DbUpdateException or
    Microsoft.Data.Sqlite.SqliteException or System.Text.Json.JsonException or FluentValidation.ValidationException)
{
    Console.Error.WriteLine("Maintenance failed: " + error.Message);
    return 1;
}

static async Task SetAdministratorAsync(string database, string email, string? name, bool create)
{
    if (!File.Exists(database)) throw new FileNotFoundException("Database does not exist.", database);
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        { DataSource = Path.GetFullPath(database), Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, Pooling = false }.ToString()).Options;
    await using var db = new ApplicationDbContext(options);
    if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Apply migrations before account maintenance.");
    var normalized = email.Trim().ToLowerInvariant();
    var existing = await db.StaffAccounts.SingleOrDefaultAsync(a => a.Email == normalized);
    if (create && existing is not null) throw new InvalidOperationException("Account already exists.");
    if (!create && (existing is null || existing.Role != StaffRole.Admin)) throw new InvalidOperationException("Administrator account does not exist.");
    var password = ReadPassword();
    var input = new CreateStaffAccountDto { Role = "Admin", Email = normalized, DisplayName = name ?? existing!.DisplayName, InitialPassword = password };
    var validation = new CreateStaffAccountValidator().Validate(input);
    if (!validation.IsValid) throw new FluentValidation.ValidationException(validation.Errors);
    var account = existing ?? new StaffAccount { Email = normalized, DisplayName = input.DisplayName, Role = StaffRole.Admin };
    account.PasswordHash = new PasswordHasher<StaffAccount>().HashPassword(account, password);
    account.IsActive = true; account.MustChangePassword = true; account.SecurityStamp = Guid.NewGuid();
    if (existing is null) db.StaffAccounts.Add(account);
    await db.SaveChangesAsync();
    Console.WriteLine("Administrator saved. A password change is required at next login; previous sessions are revoked.");
}

static string ReadPassword()
{
    if (Console.IsInputRedirected) return Console.ReadLine() ?? string.Empty;
    Console.Write("Temporary password: ");
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
        else if (!char.IsControl(key.KeyChar) && password.Length < 129) password.Append(key.KeyChar);
    }
    Console.WriteLine(); return password.ToString();
}

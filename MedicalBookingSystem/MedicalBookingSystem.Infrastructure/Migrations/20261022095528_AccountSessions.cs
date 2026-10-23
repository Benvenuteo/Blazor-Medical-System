using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "StaffAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "StaffAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "StaffAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Patients",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Patients",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            // Existing rows need distinct values in the provider's canonical GUID text format.
            foreach (var table in new[] { "Patients", "StaffAccounts" })
            {
                foreach (var column in new[] { "SecurityStamp", "Version" })
                    migrationBuilder.Sql($"UPDATE {table} SET {column} = upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)));");
            }
            // Serialize competing administrator changes at SQLite's write boundary.
            migrationBuilder.Sql("""
                CREATE TRIGGER Staff_LastAdministrator_UPDATE BEFORE UPDATE ON StaffAccounts
                WHEN OLD.Role = 'Admin' AND OLD.IsActive = 1 AND (NEW.Role <> 'Admin' OR NEW.IsActive = 0)
                    AND (SELECT COUNT(*) FROM StaffAccounts WHERE Role = 'Admin' AND IsActive = 1) <= 1
                BEGIN SELECT RAISE(ABORT, 'last_active_administrator'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Staff_LastAdministrator_DELETE BEFORE DELETE ON StaffAccounts
                WHEN OLD.Role = 'Admin' AND OLD.IsActive = 1
                    AND (SELECT COUNT(*) FROM StaffAccounts WHERE Role = 'Admin' AND IsActive = 1) <= 1
                BEGIN SELECT RAISE(ABORT, 'last_active_administrator'); END;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Staff_LastAdministrator_UPDATE;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Staff_LastAdministrator_DELETE;");
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "StaffAccounts");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "StaffAccounts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "StaffAccounts");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Patients");
        }
    }
}

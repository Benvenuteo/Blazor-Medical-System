using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_PatientId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments");

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PatientId_DoctorId",
                table: "Reviews",
                columns: new[] { "PatientId", "DoctorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_Date",
                table: "Appointments",
                columns: new[] { "DoctorId", "Date" },
                unique: true,
                filter: "Status <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId_Date",
                table: "Appointments",
                columns: new[] { "PatientId", "Date" },
                unique: true,
                filter: "Status <> 'Cancelled'");

            // Half-open interval constraints also protect legacy appointments outside the current grid.
            migrationBuilder.Sql("""
                CREATE TRIGGER Appointment_NoOverlap_INSERT BEFORE INSERT ON Appointments
                WHEN NEW.Status <> 'Cancelled' AND EXISTS (
                    SELECT 1 FROM Appointments a WHERE a.Status <> 'Cancelled'
                    AND (a.DoctorId = NEW.DoctorId OR a.PatientId = NEW.PatientId)
                    AND julianday(a.Date) < julianday(NEW.Date, '+30 minutes')
                    AND julianday(NEW.Date) < julianday(a.Date, '+30 minutes'))
                BEGIN SELECT RAISE(ABORT, 'Appointment interval conflict'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Schedule_NoOverlap_INSERT BEFORE INSERT ON DoctorSchedules
                WHEN EXISTS (SELECT 1 FROM DoctorSchedules s WHERE s.DoctorId = NEW.DoctorId
                    AND s.StartTime < NEW.EndTime AND NEW.StartTime < s.EndTime)
                BEGIN SELECT RAISE(ABORT, 'Doctor schedule overlap'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Appointment_NoOverlap_UPDATE BEFORE UPDATE ON Appointments
                WHEN NEW.Status <> 'Cancelled' AND EXISTS (
                    SELECT 1 FROM Appointments a WHERE a.Status <> 'Cancelled' AND a.Id <> NEW.Id
                    AND (a.DoctorId = NEW.DoctorId OR a.PatientId = NEW.PatientId)
                    AND julianday(a.Date) < julianday(NEW.Date, '+30 minutes')
                    AND julianday(NEW.Date) < julianday(a.Date, '+30 minutes'))
                BEGIN SELECT RAISE(ABORT, 'Appointment interval conflict'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Schedule_NoOverlap_UPDATE BEFORE UPDATE ON DoctorSchedules
                WHEN EXISTS (SELECT 1 FROM DoctorSchedules s WHERE s.DoctorId = NEW.DoctorId AND s.Id <> NEW.Id
                    AND s.StartTime < NEW.EndTime AND NEW.StartTime < s.EndTime)
                BEGIN SELECT RAISE(ABORT, 'Doctor schedule overlap'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Appointment_NoOverlap_INSERT;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Appointment_NoOverlap_UPDATE;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Schedule_NoOverlap_INSERT;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Schedule_NoOverlap_UPDATE;");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_PatientId_DoctorId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_Date",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PatientId_Date",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PatientId",
                table: "Reviews",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");
        }
    }
}

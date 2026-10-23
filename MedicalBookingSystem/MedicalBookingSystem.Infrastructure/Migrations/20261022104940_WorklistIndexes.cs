using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorklistIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_DoctorId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_DoctorId_CreatedDate_Id",
                table: "Reviews",
                columns: new[] { "DoctorId", "CreatedDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_Status_Date_Id",
                table: "Appointments",
                columns: new[] { "DoctorId", "Status", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId_Status_Date_Id",
                table: "Appointments",
                columns: new[] { "PatientId", "Status", "Date", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_DoctorId_CreatedDate_Id",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_Status_Date_Id",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PatientId_Status_Date_Id",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_DoctorId",
                table: "Reviews",
                column: "DoctorId");
        }
    }
}

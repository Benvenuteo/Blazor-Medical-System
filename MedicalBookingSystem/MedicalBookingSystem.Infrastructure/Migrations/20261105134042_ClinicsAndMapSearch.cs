using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicsAndMapSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clinics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false, collation: "NOCASE"),
                    Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorClinics",
                columns: table => new
                {
                    DoctorId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorClinics", x => new { x.DoctorId, x.ClinicId });
                    table.ForeignKey(
                        name: "FK_DoctorClinics_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoctorClinics_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // SQLite adds a column with a foreign key natively. EF would instead rebuild Appointments and
            // DoctorSchedules for AddForeignKey, and a rebuild silently drops their overlap triggers.
            migrationBuilder.Sql("ALTER TABLE \"DoctorSchedules\" ADD COLUMN \"ClinicId\" INTEGER NULL CONSTRAINT \"FK_DoctorSchedules_Clinics_ClinicId\" REFERENCES \"Clinics\" (\"Id\") ON DELETE RESTRICT;");
            migrationBuilder.Sql("ALTER TABLE \"Appointments\" ADD COLUMN \"ClinicId\" INTEGER NULL CONSTRAINT \"FK_Appointments_Clinics_ClinicId\" REFERENCES \"Clinics\" (\"Id\") ON DELETE RESTRICT;");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_ClinicId",
                table: "DoctorSchedules",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_Latitude_Longitude",
                table: "Clinics",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_Name",
                table: "Clinics",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorClinics_ClinicId",
                table: "DoctorClinics",
                column: "ClinicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back would rebuild tables that carry overlap triggers; restore a backup instead (docs/OPERATIONS.md).
            throw new NotSupportedException("ClinicsAndMapSearch cannot be reverted in place. Restore a backup taken before the upgrade.");
#pragma warning disable CS0162 // kept for reference

            migrationBuilder.DropTable(
                name: "DoctorClinics");

            migrationBuilder.DropTable(
                name: "Clinics");

            migrationBuilder.DropIndex(
                name: "IX_DoctorSchedules_ClinicId",
                table: "DoctorSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "DoctorSchedules");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "Appointments");
#pragma warning restore CS0162
        }
    }
}

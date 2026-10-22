using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ActorRole = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAudit_OccurredAtUtc_Id",
                table: "ClinicalAudit",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAudit_PatientId_OccurredAtUtc",
                table: "ClinicalAudit",
                columns: new[] { "PatientId", "OccurredAtUtc" });
            migrationBuilder.Sql("""
                CREATE TRIGGER ClinicalAudit_NoUpdate BEFORE UPDATE ON ClinicalAudit
                BEGIN SELECT RAISE(ABORT, 'clinical_audit_immutable'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER ClinicalAudit_NoDelete BEFORE DELETE ON ClinicalAudit
                BEGIN SELECT RAISE(ABORT, 'clinical_audit_immutable'); END;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ClinicalAudit_NoUpdate;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ClinicalAudit_NoDelete;");
            migrationBuilder.DropTable(
                name: "ClinicalAudit");
        }
    }
}

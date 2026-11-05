using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBookingSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoteVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRemote",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VideoRoom",
                table: "Appointments",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping a column makes EF rebuild Appointments, which removes its overlap triggers; restore a backup instead.
            throw new NotSupportedException("RemoteVisits cannot be reverted in place. Restore a backup taken before the upgrade.");
#pragma warning disable CS0162
            migrationBuilder.DropColumn(
                name: "IsRemote",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "VideoRoom",
                table: "Appointments");
#pragma warning restore CS0162
        }
    }
}

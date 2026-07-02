using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndedReservations",
                table: "FleetSnapshots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartedReservations",
                table: "FleetSnapshots",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndedReservations",
                table: "FleetSnapshots");

            migrationBuilder.DropColumn(
                name: "StartedReservations",
                table: "FleetSnapshots");
        }
    }
}

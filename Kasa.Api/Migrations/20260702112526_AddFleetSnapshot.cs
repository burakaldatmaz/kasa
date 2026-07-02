using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FleetSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalBikes = table.Column<int>(type: "INTEGER", nullable: false),
                    BrokenBikes = table.Column<int>(type: "INTEGER", nullable: false),
                    RentedBikes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetSnapshots", x => x.Id);
                    table.CheckConstraint("CK_FleetSnapshot_Capacity", "\"BrokenBikes\" + \"RentedBikes\" <= \"TotalBikes\"");
                    table.CheckConstraint("CK_FleetSnapshot_NonNegative", "\"TotalBikes\" >= 0 AND \"BrokenBikes\" >= 0 AND \"RentedBikes\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetSnapshots_Date",
                table: "FleetSnapshots",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetSnapshots");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepositReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    No = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    VehicleModel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    VehicleColor = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Plate = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AmountSatang = table.Column<long>(type: "INTEGER", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    FuelLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HandoverAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReturnExpectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LimitKmPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    LimitRadiusKm = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositReceipts", x => x.Id);
                    table.CheckConstraint("CK_DepositReceipt_AmountSatang", "\"AmountSatang\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepositReceipts_Date",
                table: "DepositReceipts",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_DepositReceipts_No",
                table: "DepositReceipts",
                column: "No",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepositReceipts");
        }
    }
}

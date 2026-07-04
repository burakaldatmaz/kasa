using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasa.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositReceiptExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut kayıtlar (eski sabit "150 km/gün · within-150" davranışı) için backfill.
            migrationBuilder.AddColumn<int>(
                name: "DailyKm",
                table: "DepositReceipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 150);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "DepositReceipts",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RadiusPolicy",
                table: "DepositReceipts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "within-150");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNo",
                table: "DepositReceipts",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "DepositReceipts",
                type: "TEXT",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyKm",
                table: "DepositReceipts");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "DepositReceipts");

            migrationBuilder.DropColumn(
                name: "RadiusPolicy",
                table: "DepositReceipts");

            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "DepositReceipts");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "DepositReceipts");
        }
    }
}

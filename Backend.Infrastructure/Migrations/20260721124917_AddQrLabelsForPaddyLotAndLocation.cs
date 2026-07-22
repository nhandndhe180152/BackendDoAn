using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQrLabelsForPaddyLotAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add QrCode columns as nullable first
            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "PaddyLot",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "Location",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // 2. Dynamic backfill with UUID for existing records (no hardcoding)
            migrationBuilder.Sql("UPDATE PaddyLot SET QrCode = CONCAT('PL-', UPPER(REPLACE(UUID(), '-', ''))) WHERE QrCode IS NULL OR QrCode = '';");
            migrationBuilder.Sql("UPDATE Location SET QrCode = CONCAT('LC-', UPPER(REPLACE(UUID(), '-', ''))) WHERE QrCode IS NULL OR QrCode = '';");

            // 3. Alter columns to nullable: false
            migrationBuilder.AlterColumn<string>(
                name: "QrCode",
                table: "PaddyLot",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "QrCode",
                table: "Location",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // 4. Create unique indexes
            migrationBuilder.CreateIndex(
                name: "UX_PaddyLot_QrCode",
                table: "PaddyLot",
                column: "QrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Location_QrCode",
                table: "Location",
                column: "QrCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PaddyLot_QrCode",
                table: "PaddyLot");

            migrationBuilder.DropIndex(
                name: "UX_Location_QrCode",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "PaddyLot");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Location");
        }
    }
}

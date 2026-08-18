using Microsoft.EntityFrameworkCore.Migrations;
using Backend.Application.Constants;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuarantinedInventoryBreakdownSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "LotStatus",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: LotStatusCodeConstants.PendingInbound);

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: LotStatusCodeConstants.InStock);

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: LotStatusCodeConstants.Processing);

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: LotStatusCodeConstants.Quarantine);

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: LotStatusCodeConstants.Milling);

            migrationBuilder.UpdateData(
                table: "LotStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: LotStatusCodeConstants.Depleted);

            migrationBuilder.CreateIndex(
                name: "UX_LotStatus_Code",
                table: "LotStatus",
                column: "Code",
                unique: true);

            migrationBuilder.Sql($"UPDATE LotStatus SET IsSellable = 0 WHERE Code = '{LotStatusCodeConstants.Quarantine}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LotStatus_Code",
                table: "LotStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "LotStatus");
        }
    }
}

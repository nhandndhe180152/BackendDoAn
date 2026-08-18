using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePartiallyStockedColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Color",
                value: "#06B6D4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Color",
                value: "#3B82F6");
        }
    }
}

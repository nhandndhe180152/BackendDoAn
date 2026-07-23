using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMillingOrderStatusCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "MillingOrderStatus",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "RESERVED");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "IN_PROGRESS");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "AWAITING_OUTPUT");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "MillingOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: "CANCELLED");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "MillingOrderStatus");
        }
    }
}

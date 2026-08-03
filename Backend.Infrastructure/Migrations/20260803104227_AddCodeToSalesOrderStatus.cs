using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeToSalesOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "SalesOrderStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "NEW");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "PENDING_CONFIRM");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "RESERVED");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "AWAITING_MILLING");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "PREPARING");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: "DELIVERING");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Code",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "SalesOrderStatus",
                keyColumn: "Id",
                keyValue: 8,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.Sql("UPDATE `SalesOrderStatus` SET `Code` = CONCAT('SO_', Id) WHERE `Code` = '' OR `Code` IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderStatus_Code",
                table: "SalesOrderStatus",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrderStatus_Code",
                table: "SalesOrderStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "SalesOrderStatus");
        }
    }
}

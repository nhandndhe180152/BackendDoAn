using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranslatePurchaseOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Nháp");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Đã xác nhận");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Nhận một phần");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Đã nhận hàng");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Đã hủy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Draft");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Confirmed");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "PartiallyReceived");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Received");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Cancelled");
        }
    }
}

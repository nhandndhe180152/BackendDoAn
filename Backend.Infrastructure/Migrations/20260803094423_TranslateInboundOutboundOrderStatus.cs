using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranslateInboundOutboundOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO InboundOrderStatus (Id, Color, CreatedDate, IsDeleted, Name)
VALUES 
(1, '#6B7280', '2026-01-01 00:00:00', 0, 'Nháp'),
(2, '#3B82F6', '2026-01-01 00:00:00', 0, 'Chờ duyệt'),
(3, '#8B5CF6', '2026-01-01 00:00:00', 0, 'Đã duyệt'),
(4, '#EF4444', '2026-01-01 00:00:00', 0, 'Từ chối'),
(5, '#06B6D4', '2026-01-01 00:00:00', 0, 'Đang nhận hàng'),
(6, '#F59E0B', '2026-01-01 00:00:00', 0, 'Nhận một phần'),
(7, '#10B981', '2026-01-01 00:00:00', 0, 'Đã nhận hàng'),
(8, '#EF4444', '2026-01-01 00:00:00', 0, 'Đã hủy')
ON DUPLICATE KEY UPDATE 
Name = VALUES(Name), 
Color = VALUES(Color);
");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Nháp");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Đang lấy hàng");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Đã đóng gói");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Đang giao hàng");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Hoàn thành");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Đã hủy");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "Giao hàng thất bại");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "PICKING");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "PACKED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "DISPATCHED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "CANCELLED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "DELIVERY_FAILED");
        }
    }
}

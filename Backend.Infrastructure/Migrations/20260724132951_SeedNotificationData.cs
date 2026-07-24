using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedNotificationData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT IGNORE INTO NotificationCategory (Id, Color, CreatedBy, Description, LastModifiedDate, Name, UpdatedBy) VALUES " +
                "(1, '#ef4444', NULL, 'Thông báo liên quan đến hàng tồn kho thấp cần bổ sung', NULL, 'Cảnh báo tồn kho thấp', NULL), " +
                "(2, '#0ea5e9', NULL, 'Thông báo liên quan đến lịch và phiếu thu mua lúa', NULL, 'Thu mua lúa', NULL), " +
                "(3, '#10b981', NULL, 'Thông báo đơn mua hàng hóa khác', NULL, 'Đơn mua hàng', NULL), " +
                "(4, '#3b82f6', NULL, 'Thông báo quy trình nhập kho', NULL, 'Đơn nhập kho', NULL), " +
                "(5, '#3b82f6', NULL, 'Thông báo quy trình xay xát lúa', NULL, 'Xay xát', NULL), " +
                "(6, '#f59e0b', NULL, 'Thông báo quy trình bán hàng', NULL, 'Đơn bán hàng', NULL), " +
                "(7, '#10b981', NULL, 'Thông báo quy trình xuất kho', NULL, 'Đơn xuất kho', NULL), " +
                "(8, '#10b981', NULL, 'Thông báo điều chuyển nội bộ', NULL, 'Điều chuyển kho', NULL), " +
                "(9, '#10b981', NULL, 'Thông báo quá trình kiểm kê', NULL, 'Kiểm kê kho', NULL), " +
                "(10, '#ef4444', NULL, 'Thông báo kiểm tra chất lượng và cảnh báo lô', NULL, 'Kiểm định chất lượng', NULL), " +
                "(11, '#ef4444', NULL, 'Thông báo chung và cảnh báo lỗi từ hệ thống', NULL, 'Hệ thống', NULL);");

            migrationBuilder.Sql("INSERT IGNORE INTO NotificationType (Id, CreatedBy, Description, LastModifiedDate, Name, UpdatedBy) VALUES " +
                "(1, NULL, 'Thông báo tự động từ hệ thống', NULL, 'Hệ thống', NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "NotificationCategory",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "NotificationType",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}

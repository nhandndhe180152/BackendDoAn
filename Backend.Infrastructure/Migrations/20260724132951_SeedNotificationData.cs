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
            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#ef4444', 'Thông báo liên quan đến hàng tồn kho thấp cần bổ sung', 'Cảnh báo tồn kho thấp' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Cảnh báo tồn kho thấp');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#0ea5e9', 'Thông báo liên quan đến lịch và phiếu thu mua lúa', 'Thu mua lúa' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Thu mua lúa');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#10b981', 'Thông báo đơn mua hàng hóa khác', 'Đơn mua hàng' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Đơn mua hàng');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#3b82f6', 'Thông báo quy trình nhập kho', 'Đơn nhập kho' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Đơn nhập kho');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#3b82f6', 'Thông báo quy trình xay xát lúa', 'Xay xát' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Xay xát');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#f59e0b', 'Thông báo quy trình bán hàng', 'Đơn bán hàng' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Đơn bán hàng');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#10b981', 'Thông báo quy trình xuất kho', 'Đơn xuất kho' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Đơn xuất kho');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#10b981', 'Thông báo điều chuyển nội bộ', 'Điều chuyển kho' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Điều chuyển kho');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#10b981', 'Thông báo quá trình kiểm kê', 'Kiểm kê kho' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Kiểm kê kho');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#ef4444', 'Thông báo kiểm tra chất lượng và cảnh báo lô', 'Kiểm định chất lượng' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Kiểm định chất lượng');");

            migrationBuilder.Sql("INSERT INTO NotificationCategory (Color, Description, Name) " +
                "SELECT '#ef4444', 'Thông báo chung và cảnh báo lỗi từ hệ thống', 'Hệ thống' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationCategory WHERE Name = 'Hệ thống');");


            migrationBuilder.Sql("INSERT INTO NotificationType (Description, Name) " +
                "SELECT 'Thông báo tự động từ hệ thống', 'Hệ thống' " +
                "FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM NotificationType WHERE Name = 'Hệ thống');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM NotificationCategory WHERE Name IN (" +
                "'Cảnh báo tồn kho thấp', 'Thu mua lúa', 'Đơn mua hàng', 'Đơn nhập kho', " +
                "'Xay xát', 'Đơn bán hàng', 'Đơn xuất kho', 'Điều chuyển kho', " +
                "'Kiểm kê kho', 'Kiểm định chất lượng', 'Hệ thống');");

            migrationBuilder.Sql("DELETE FROM NotificationType WHERE Name = 'Hệ thống';");
        }
    }
}

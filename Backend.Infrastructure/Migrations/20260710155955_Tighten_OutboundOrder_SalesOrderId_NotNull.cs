using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Tighten_OutboundOrder_SalesOrderId_NotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill an toàn: xóa OutboundOrder chưa có SalesOrderId (không thể tạo placeholder hợp lệ).
            // CẢNH BÁO: Nếu DB đã có phiếu xuất thực tế, chạy backfill thủ công trước:
            //   UPDATE OutboundOrder SET SalesOrderId = <Id hợp lệ> WHERE SalesOrderId IS NULL;
            migrationBuilder.Sql(
                "DELETE FROM `OutboundOrder` WHERE `SalesOrderId` IS NULL;",
                suppressTransaction: true);

            migrationBuilder.AlterColumn<int>(
                name: "SalesOrderId",
                table: "OutboundOrder",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SalesOrderId",
                table: "OutboundOrder",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}

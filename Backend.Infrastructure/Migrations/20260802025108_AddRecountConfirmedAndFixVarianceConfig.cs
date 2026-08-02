using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecountConfirmedAndFixVarianceConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_WarehouseId",
                table: "InventoryTransaction");

            migrationBuilder.AddColumn<bool>(
                name: "RecountConfirmed",
                table: "StockTakeItem",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecountConfirmedAt",
                table: "StockTakeItem",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecountConfirmedBy",
                table: "StockTakeItem",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItem_RecountConfirmedBy",
                table: "StockTakeItem",
                column: "RecountConfirmedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItem_User_RecountConfirmedBy",
                table: "StockTakeItem",
                column: "RecountConfirmedBy",
                principalTable: "User",
                principalColumn: "Id");

            // ── Seed ngưỡng kg mới (MySQL: INSERT IGNORE để tránh trùng key) ──────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch SMALL khi kiểm kê (kg)', 'StockTakeSmallVarianceKg', '5', 0)");

            migrationBuilder.Sql(@"
                INSERT IGNORE INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch MEDIUM khi kiểm kê (kg)', 'StockTakeMediumVarianceKg', '20', 0)");

            // ── Cập nhật ngưỡng % cũ về đúng giá trị FDS (0.5% và 2%) ───────────────
            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '0.5'
                WHERE ConfigKey = 'StockTakeSmallVariancePercent' AND ConfigValue = '1'");

            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '2'
                WHERE ConfigKey = 'StockTakeMediumVariancePercent' AND ConfigValue = '5'");

            // Chèn dự phòng nếu chưa tồn tại (môi trường mới chưa có seed cũ)
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch SMALL khi kiểm kê (%)', 'StockTakeSmallVariancePercent', '0.5', 0)");

            migrationBuilder.Sql(@"
                INSERT IGNORE INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch MEDIUM khi kiểm kê (%)', 'StockTakeMediumVariancePercent', '2', 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItem_User_RecountConfirmedBy",
                table: "StockTakeItem");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeItem_RecountConfirmedBy",
                table: "StockTakeItem");

            migrationBuilder.DropColumn(
                name: "RecountConfirmed",
                table: "StockTakeItem");

            migrationBuilder.DropColumn(
                name: "RecountConfirmedAt",
                table: "StockTakeItem");

            migrationBuilder.DropColumn(
                name: "RecountConfirmedBy",
                table: "StockTakeItem");

            // Rollback ngưỡng kg
            migrationBuilder.Sql("DELETE FROM SystemConfig WHERE ConfigKey IN ('StockTakeSmallVarianceKg','StockTakeMediumVarianceKg')");

            // Rollback ngưỡng % về giá trị cũ (chỉ rollback nếu đang ở giá trị mới)
            migrationBuilder.Sql("UPDATE SystemConfig SET ConfigValue = '1' WHERE ConfigKey = 'StockTakeSmallVariancePercent' AND ConfigValue = '0.5'");
            migrationBuilder.Sql("UPDATE SystemConfig SET ConfigValue = '5' WHERE ConfigKey = 'StockTakeMediumVariancePercent' AND ConfigValue = '2'");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_WarehouseId",
                table: "InventoryTransaction",
                column: "WarehouseId");
        }
    }
}

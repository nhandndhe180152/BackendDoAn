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

            // ── Bước 1: Dọn dàng trùng ConfigKey có thể tồn tại trước khi tạo unique index ────────────────
            // Giữ lại 1 dòng có Id nhỏ nhất (dòng đầu tiên được seed), xóa các dòng trùng còn lại.
            migrationBuilder.Sql(@"
                DELETE sc FROM SystemConfig sc
                INNER JOIN (
                    SELECT ConfigKey, MIN(Id) AS KeepId
                    FROM SystemConfig
                    GROUP BY ConfigKey
                    HAVING COUNT(*) > 1
                ) dup ON sc.ConfigKey = dup.ConfigKey AND sc.Id <> dup.KeepId;");

            // ── Bước 2: Tạo unique index trên ConfigKey (dùng migrationBuilder để EF tracking snapshot) ──────
            migrationBuilder.CreateIndex(
                name: "UX_SystemConfig_ConfigKey",
                table: "SystemConfig",
                column: "ConfigKey",
                unique: true);

            // ── Bước 3: Seed ngưỡng kg mới (có unique index rồi, dùng ON DUPLICATE KEY UPDATE cho an toàn) ───
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch SMALL khi kiểm kê (kg)', 'StockTakeSmallVarianceKg', '5', 0)
                ON DUPLICATE KEY UPDATE ConfigValue = VALUES(ConfigValue);");

            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch MEDIUM khi kiểm kê (kg)', 'StockTakeMediumVarianceKg', '20', 0)
                ON DUPLICATE KEY UPDATE ConfigValue = VALUES(ConfigValue);");

            // ── Bước 4: Cập nhật ngưỡng % cũ về đúng giá trị FDS (0.5% và 2%) ────────────────────────────────
            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '0.5'
                WHERE ConfigKey = 'StockTakeSmallVariancePercent' AND ConfigValue = '1';");

            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '2'
                WHERE ConfigKey = 'StockTakeMediumVariancePercent' AND ConfigValue = '5';");

            // ── Bước 5: Seed % dự phòng (môi trường mới chưa có seed cũ) dùng ON DUPLICATE KEY UPDATE ─────────
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch SMALL khi kiểm kê (%)', 'StockTakeSmallVariancePercent', '0.5', 0)
                ON DUPLICATE KEY UPDATE ConfigValue = VALUES(ConfigValue);");

            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                VALUES ('Ngưỡng chênh lệch MEDIUM khi kiểm kê (%)', 'StockTakeMediumVariancePercent', '2', 0)
                ON DUPLICATE KEY UPDATE ConfigValue = VALUES(ConfigValue);");
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

            // Rollback unique index
            migrationBuilder.DropIndex(
                name: "UX_SystemConfig_ConfigKey",
                table: "SystemConfig");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_WarehouseId",
                table: "InventoryTransaction",
                column: "WarehouseId");
        }
    }
}

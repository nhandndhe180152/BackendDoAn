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
            // ── 1. Thêm cột xác nhận kiểm đếm lại vào StockTakeItem ──────────
            migrationBuilder.AddColumn<bool>(
                name: "RecountConfirmed",
                table: "StockTakeItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecountConfirmedBy",
                table: "StockTakeItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecountConfirmedAt",
                table: "StockTakeItem",
                type: "datetime2",
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

            // ── 2. Seed ngưỡng kg mới (chỉ chèn nếu chưa tồn tại để tránh key trùng) ─
            // StockTakeSmallVarianceKg = 5 kg
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'StockTakeSmallVarianceKg')
                BEGIN
                    INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                    VALUES (N'Ngưỡng chênh lệch SMALL khi kiểm kê (kg)', 'StockTakeSmallVarianceKg', '5', 0)
                END");

            // StockTakeMediumVarianceKg = 20 kg
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'StockTakeMediumVarianceKg')
                BEGIN
                    INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                    VALUES (N'Ngưỡng chênh lệch MEDIUM khi kiểm kê (kg)', 'StockTakeMediumVarianceKg', '20', 0)
                END");

            // ── 3. Cập nhật ngưỡng % cũ về đúng giá trị FDS (0.5% và 2%) ───
            // Dùng UPDATE thay vì INSERT để tránh key trùng nếu seed cũ đã có
            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '0.5'
                WHERE ConfigKey = 'StockTakeSmallVariancePercent' AND ConfigValue = '1'");

            migrationBuilder.Sql(@"
                UPDATE SystemConfig SET ConfigValue = '2'
                WHERE ConfigKey = 'StockTakeMediumVariancePercent' AND ConfigValue = '5'");

            // Chèn dự phòng nếu chưa tồn tại (môi trường mới chưa có seed cũ)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'StockTakeSmallVariancePercent')
                BEGIN
                    INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                    VALUES (N'Ngưỡng chênh lệch SMALL khi kiểm kê (%)', 'StockTakeSmallVariancePercent', '0.5', 0)
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'StockTakeMediumVariancePercent')
                BEGIN
                    INSERT INTO SystemConfig (Name, ConfigKey, ConfigValue, IsDeleted)
                    VALUES (N'Ngưỡng chênh lệch MEDIUM khi kiểm kê (%)', 'StockTakeMediumVariancePercent', '2', 0)
                END");
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

            migrationBuilder.DropColumn(name: "RecountConfirmed",    table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "RecountConfirmedBy",  table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "RecountConfirmedAt",  table: "StockTakeItem");

            // Rollback ngưỡng kg
            migrationBuilder.Sql("DELETE FROM SystemConfig WHERE ConfigKey IN ('StockTakeSmallVarianceKg','StockTakeMediumVarianceKg')");

            // Rollback ngưỡng % về giá trị cũ
            migrationBuilder.Sql("UPDATE SystemConfig SET ConfigValue = '1'  WHERE ConfigKey = 'StockTakeSmallVariancePercent'");
            migrationBuilder.Sql("UPDATE SystemConfig SET ConfigValue = '5'  WHERE ConfigKey = 'StockTakeMediumVariancePercent'");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class W14J_AddScaleTraceabilityToPaddyLotBag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // W14-J: Thêm các cột truy vết cân vào bảng PaddyLotBag
            migrationBuilder.AddColumn<string>(
                name: "ScaleDeviceRef",
                table: "PaddyLotBag",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightCaptureMethod",
                table: "PaddyLotBag",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeighedAt",
                table: "PaddyLotBag",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeighedBy",
                table: "PaddyLotBag",
                type: "int",
                nullable: true);

            // W14-J: Seed cấu hình ngưỡng độ ẩm nhập kho vào SystemConfig
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'ReceivingQcMoistureMinPercent', '10', 'Ngưỡng độ ẩm tối thiểu khi nhận lúa (%)',
                       'Giá trị độ ẩm tối thiểu hợp lệ khi tiếp nhận lúa tại kho (Receiving QC). Dưới ngưỡng này, lúa quá khô.', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'ReceivingQcMoistureMinPercent');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'ReceivingQcMoistureMaxPercent', '28', 'Ngưỡng độ ẩm tối đa khi nhận lúa (%)',
                       'Giá trị độ ẩm tối đa hợp lệ khi tiếp nhận lúa tại kho (Receiving QC). Vượt ngưỡng này cần cách ly hoặc từ chối.', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'ReceivingQcMoistureMaxPercent');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'StorageQcMoistureWarningPercent', '14.5', 'Ngưỡng cảnh báo độ ẩm trong kho (%)',
                       'Ngưỡng độ ẩm cảnh báo (periodic QC) trong quá trình lưu kho. Khác với ngưỡng nhận lúa Receiving QC.', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'StorageQcMoistureWarningPercent');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ScaleDeviceRef",      table: "PaddyLotBag");
            migrationBuilder.DropColumn(name: "WeightCaptureMethod", table: "PaddyLotBag");
            migrationBuilder.DropColumn(name: "WeighedAt",           table: "PaddyLotBag");
            migrationBuilder.DropColumn(name: "WeighedBy",           table: "PaddyLotBag");

            migrationBuilder.Sql(@"
                DELETE FROM SystemConfig
                WHERE ConfigKey IN ('ReceivingQcMoistureMinPercent','ReceivingQcMoistureMaxPercent','StorageQcMoistureWarningPercent');
            ");
        }
    }
}

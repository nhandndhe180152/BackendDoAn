using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaddyLotIdToStockTakeItemAndVarianceConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaddyLotId",
                table: "StockTakeItem",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItem_PaddyLotId",
                table: "StockTakeItem",
                column: "PaddyLotId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItem_PaddyLot_PaddyLotId",
                table: "StockTakeItem",
                column: "PaddyLotId",
                principalTable: "PaddyLot",
                principalColumn: "Id");

            // Seed ngưỡng phân loại chênh lệch kiểm kê kho vào SystemConfig.
            // StockTakeService đọc 2 key này tại runtime; không hard-code trong code.
            // Admin có thể chỉnh sửa giá trị trong màn "Cấu hình hệ thống".
            migrationBuilder.InsertData(
                table: "SystemConfig",
                columns: new[] { "Name", "ConfigKey", "ConfigValue", "IsDeleted" },
                values: new object[,]
                {
                    {
                        "Ngưỡng chênh lệch SMALL khi kiểm kê (%)",
                        "StockTakeSmallVariancePercent",
                        "1",
                        false
                    },
                    {
                        "Ngưỡng chênh lệch MEDIUM khi kiểm kê (%)",
                        "StockTakeMediumVariancePercent",
                        "5",
                        false
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemConfig",
                keyColumn: "ConfigKey",
                keyValue: "StockTakeSmallVariancePercent");

            migrationBuilder.DeleteData(
                table: "SystemConfig",
                keyColumn: "ConfigKey",
                keyValue: "StockTakeMediumVariancePercent");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItem_PaddyLot_PaddyLotId",
                table: "StockTakeItem");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeItem_PaddyLotId",
                table: "StockTakeItem");

            migrationBuilder.DropColumn(
                name: "PaddyLotId",
                table: "StockTakeItem");
        }
    }
}

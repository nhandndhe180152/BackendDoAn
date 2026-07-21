using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteJob01LowStockDetectionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockAlertConfig_LowStockLookup",
                table: "StockAlertConfig",
                columns: new[] { "WarehouseId", "ProductVariantId", "IsActive", "IsDeleted" });

            migrationBuilder.DropIndex(
                name: "IX_StockAlertConfig_WarehouseId",
                table: "StockAlertConfig");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinThreshold",
                table: "StockAlertConfig",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "Alert",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_LowStockAggregation",
                table: "Inventory",
                columns: new[] { "WarehouseId", "ProductVariantId", "IsDeleted", "LocationId", "PaddyLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alert_ActiveLowStockLookup",
                table: "Alert",
                columns: new[] { "AlertType", "WarehouseId", "ProductVariantId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_Alert_DeduplicationKey",
                table: "Alert",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.Sql("UPDATE LotStatus SET IsSellable = 0 WHERE Name = 'Cách ly';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockAlertConfig_WarehouseId",
                table: "StockAlertConfig",
                column: "WarehouseId");

            migrationBuilder.DropIndex(
                name: "IX_StockAlertConfig_LowStockLookup",
                table: "StockAlertConfig");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_LowStockAggregation",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Alert_ActiveLowStockLookup",
                table: "Alert");

            migrationBuilder.DropIndex(
                name: "UX_Alert_DeduplicationKey",
                table: "Alert");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "Alert");

            migrationBuilder.AlterColumn<int>(
                name: "MinThreshold",
                table: "StockAlertConfig",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);
        }
    }
}

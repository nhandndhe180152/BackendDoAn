using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMillingActualMetricsAndRiceVariety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualPaddyInputKg",
                table: "MillingOrder",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualYieldRate",
                table: "MillingOrder",
                type: "decimal(8,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiceVarietyId",
                table: "MillingOrder",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_RiceVarietyId",
                table: "MillingOrder",
                column: "RiceVarietyId");

            migrationBuilder.AddForeignKey(
                name: "FK_MillingOrder_RiceVariety_RiceVarietyId",
                table: "MillingOrder",
                column: "RiceVarietyId",
                principalTable: "RiceVariety",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
                UPDATE MillingOrder mo
                JOIN (
                    SELECT so.Id AS SalesOrderId, MIN(pv.RiceVarietyId) AS RiceVarietyId
                    FROM SalesOrder so
                    JOIN SalesOrderItem soi ON soi.SalesOrderId = so.Id AND soi.IsDeleted = 0
                    JOIN ProductVariant pv ON pv.Id = soi.ProductVariantId
                    WHERE pv.RiceVarietyId IS NOT NULL
                    GROUP BY so.Id
                    HAVING COUNT(DISTINCT pv.RiceVarietyId) = 1
                ) src ON src.SalesOrderId = mo.SalesOrderId
                SET mo.RiceVarietyId = src.RiceVarietyId
                WHERE mo.RiceVarietyId IS NULL;");

            migrationBuilder.Sql(@"
                UPDATE MillingOrder mo
                JOIN (
                    SELECT mi.MillingOrderId, MIN(pl.RiceVarietyId) AS RiceVarietyId
                    FROM MillingOrderInput mi
                    JOIN PaddyLot pl ON pl.Id = mi.PaddyLotId
                    WHERE mi.IsDeleted = 0 AND pl.RiceVarietyId IS NOT NULL
                    GROUP BY mi.MillingOrderId
                    HAVING COUNT(DISTINCT pl.RiceVarietyId) = 1
                ) src ON src.MillingOrderId = mo.Id
                SET mo.RiceVarietyId = src.RiceVarietyId
                WHERE mo.RiceVarietyId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MillingOrder_RiceVariety_RiceVarietyId",
                table: "MillingOrder");

            migrationBuilder.DropIndex(
                name: "IX_MillingOrder_RiceVarietyId",
                table: "MillingOrder");

            migrationBuilder.DropColumn(
                name: "ActualPaddyInputKg",
                table: "MillingOrder");

            migrationBuilder.DropColumn(
                name: "ActualYieldRate",
                table: "MillingOrder");

            migrationBuilder.DropColumn(
                name: "RiceVarietyId",
                table: "MillingOrder");
        }
    }
}

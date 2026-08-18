using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_OutboundOrder_ShadowFKColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId1",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_WarehouseId1",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "WarehouseId1",
                table: "OutboundOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OutboundOrderStatusId1",
                table: "OutboundOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId1",
                table: "OutboundOrder",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_OutboundOrderStatusId1",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId1");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_WarehouseId1",
                table: "OutboundOrder",
                column: "WarehouseId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId1",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId1",
                principalTable: "OutboundOrderStatus",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId1",
                table: "OutboundOrder",
                column: "WarehouseId1",
                principalTable: "Warehouse",
                principalColumn: "Id");
        }
    }
}

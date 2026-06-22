using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInventoryInboundOrderId_AddRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_InboundOrder_InboundOrderId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_InboundOrderId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "InboundOrderId",
                table: "Inventory");

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "Inventory",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Inventory");

            migrationBuilder.AddColumn<int>(
                name: "InboundOrderId",
                table: "Inventory",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_InboundOrderId",
                table: "Inventory",
                column: "InboundOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_InboundOrder_InboundOrderId",
                table: "Inventory",
                column: "InboundOrderId",
                principalTable: "InboundOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

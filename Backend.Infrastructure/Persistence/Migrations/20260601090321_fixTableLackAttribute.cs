using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class fixTableLackAttribute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinStockLevel",
                table: "ProductVariant",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Inventory",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedDate",
                table: "Inventory",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariant_MinStockLevel",
                table: "ProductVariant",
                column: "MinStockLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_LocationId",
                table: "Inventory",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_Inventory_ProductVariant_Warehouse_Location",
                table: "Inventory",
                columns: new[] { "ProductVariantId", "WarehouseId", "LocationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Location_LocationId",
                table: "Inventory",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Location_LocationId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariant_MinStockLevel",
                table: "ProductVariant");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_LocationId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "UX_Inventory_ProductVariant_Warehouse_Location",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "MinStockLevel",
                table: "ProductVariant");

            migrationBuilder.DropColumn(
                name: "LastModifiedDate",
                table: "Inventory");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Inventory",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductVariantId",
                table: "Inventory",
                column: "ProductVariantId");
        }
    }
}

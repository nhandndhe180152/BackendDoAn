using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTransactionCreatedDateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_CreatedDate",
                table: "InventoryTransaction",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_Warehouse_CreatedDate",
                table: "InventoryTransaction",
                columns: new[] { "WarehouseId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_CreatedDate",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_Warehouse_CreatedDate",
                table: "InventoryTransaction");
        }
    }
}

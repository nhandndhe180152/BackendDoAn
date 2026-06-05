using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class fixTableLackAttributev2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "WeightKg",
                table: "InventoryTransaction",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionType",
                table: "InventoryTransaction",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "InventoryTransaction",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "InventoryTransaction",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityReserved",
                table: "Inventory",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrderId",
                table: "Inventory",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPrice",
                table: "Inventory",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_ProductVariant_ProductVariantId",
                table: "Inventory",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_PurchaseOrder_PurchaseOrderId",
                table: "Inventory",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Warehouse_WarehouseId",
                table: "Inventory",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_Inventory_InventoryId",
                table: "InventoryTransaction",
                column: "InventoryId",
                principalTable: "Inventory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_IotWeightLog_IotWeightLogId",
                table: "InventoryTransaction",
                column: "IotWeightLogId",
                principalTable: "IotWeightLog",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_Location_LocationId",
                table: "InventoryTransaction",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_ProductVariant_ProductVariantId",
                table: "InventoryTransaction",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_Warehouse_WarehouseId",
                table: "InventoryTransaction",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_ProductVariant_ProductVariantId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_PurchaseOrder_PurchaseOrderId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Warehouse_WarehouseId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_Inventory_InventoryId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_IotWeightLog_IotWeightLogId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_Location_LocationId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_ProductVariant_ProductVariantId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_Warehouse_WarehouseId",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_InventoryId",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_IotWeightLogId",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_LocationId",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_ProductVariant_CreatedDate",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_Reference",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductVariantId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "InventoryId",
                table: "InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "IotWeightLogId",
                table: "InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "ReferenceItemId",
                table: "InventoryTransaction");

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightKg",
                table: "InventoryTransaction",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionType",
                table: "InventoryTransaction",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "InventoryTransaction",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "InventoryTransaction",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityReserved",
                table: "Inventory",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrderId",
                table: "Inventory",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPrice",
                table: "Inventory",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_ProductVariantId",
                table: "InventoryTransaction",
                column: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_ProductVariant_ProductVariantId",
                table: "Inventory",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_PurchaseOrder_PurchaseOrderId",
                table: "Inventory",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Warehouse_WarehouseId",
                table: "Inventory",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_ProductVariant_ProductVariantId",
                table: "InventoryTransaction",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_Warehouse_WarehouseId",
                table: "InventoryTransaction",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

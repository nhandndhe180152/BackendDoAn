using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePurchaseSalesOrderToInboundOutbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create helper procedures for safe schema modifications in MySQL
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS DropForeignKeyIfExists;
CREATE PROCEDURE DropForeignKeyIfExists(IN tblName VARCHAR(100), IN fkName VARCHAR(100))
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = tblName AND CONSTRAINT_NAME = fkName AND CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) THEN
        SET @sql = CONCAT('ALTER TABLE `', tblName, '` DROP FOREIGN KEY `', fkName, '`');
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END;");

            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS RenameTableIfExists;
CREATE PROCEDURE RenameTableIfExists(IN oldTbl VARCHAR(100), IN newTbl VARCHAR(100))
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = oldTbl
    ) AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = newTbl
    ) THEN
        SET @sql = CONCAT('RENAME TABLE `', oldTbl, '` TO `', newTbl, '`');
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END;");

            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS RenameColumnIfExists;
CREATE PROCEDURE RenameColumnIfExists(IN tblName VARCHAR(100), IN oldCol VARCHAR(100), IN newCol VARCHAR(100))
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tblName AND COLUMN_NAME = oldCol
    ) THEN
        SET @sql = CONCAT('ALTER TABLE `', tblName, '` RENAME COLUMN `', oldCol, '` TO `', newCol, '`');
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END;");

            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS RenameIndexIfExists;
CREATE PROCEDURE RenameIndexIfExists(IN tblName VARCHAR(100), IN oldIdx VARCHAR(100), IN newIdx VARCHAR(100))
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS 
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tblName AND INDEX_NAME = oldIdx
    ) THEN
        SET @sql = CONCAT('ALTER TABLE `', tblName, '` RENAME INDEX `', oldIdx, '` TO `', newIdx, '`');
        PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END;");

            // 2. Safe drop of old foreign keys
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('Inventory', 'FK_Inventory_PurchaseOrder_PurchaseOrderId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrder', 'FK_PurchaseOrder_DeliveryNote_DeliveryNoteId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrder', 'FK_PurchaseOrder_PurchaseOrderStatus_PurchaseOrderStatusId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrder', 'FK_PurchaseOrder_Supplier_SupplierId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrder', 'FK_PurchaseOrder_Warehouse_WarehouseId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrderItem', 'FK_PurchaseOrderItem_ProductVariant_ProductVariantId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('PurchaseOrderItem', 'FK_PurchaseOrderItem_PurchaseOrder_PurchaseOrderId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('SalesOrder', 'FK_SalesOrder_SalesOrderStatus_SalesOrderStatusId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('SalesOrder', 'FK_SalesOrder_User_AssignedUserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('SalesOrder', 'FK_SalesOrder_Warehouse_WarehouseId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('SalesOrderItem', 'FK_SalesOrderItem_ProductVariant_ProductVariantId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('SalesOrderItem', 'FK_SalesOrderItem_SalesOrder_SalesOrderId');");

            // 3. Safe rename tables
            migrationBuilder.Sql("CALL RenameTableIfExists('PurchaseOrder', 'InboundOrder');");
            migrationBuilder.Sql("CALL RenameTableIfExists('PurchaseOrderItem', 'InboundOrderItem');");
            migrationBuilder.Sql("CALL RenameTableIfExists('PurchaseOrderStatus', 'InboundOrderStatus');");
            migrationBuilder.Sql("CALL RenameTableIfExists('SalesOrder', 'OutboundOrder');");
            migrationBuilder.Sql("CALL RenameTableIfExists('SalesOrderItem', 'OutboundOrderItem');");
            migrationBuilder.Sql("CALL RenameTableIfExists('SalesOrderStatus', 'OutboundOrderStatus');");

            // 4. Safe rename columns
            migrationBuilder.Sql("CALL RenameColumnIfExists('InboundOrder', 'TotalAmount', 'TotalAssetValue');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('OutboundOrder', 'TotalAmount', 'TotalDispatchedValue');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('OutboundOrder', 'TotalCostAmount', 'TotalDispatchedValue');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('InboundOrderItem', 'PurchaseOrderId', 'InboundOrderId');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('OutboundOrderItem', 'SalesOrderId', 'OutboundOrderId');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('Inventory', 'PurchaseOrderId', 'InboundOrderId');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('DeliveryNote', 'PurchaseOrderId', 'InboundOrderId');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('InboundOrder', 'PurchaseOrderStatusId', 'InboundOrderStatusId');");
            migrationBuilder.Sql("CALL RenameColumnIfExists('OutboundOrder', 'SalesOrderStatusId', 'OutboundOrderStatusId');");

            // 5. Safe rename indexes
            migrationBuilder.Sql("CALL RenameIndexIfExists('Inventory', 'IX_Inventory_PurchaseOrderId', 'IX_Inventory_InboundOrderId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrder', 'IX_PurchaseOrder_DeliveryNoteId', 'IX_InboundOrder_DeliveryNoteId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrder', 'IX_PurchaseOrder_PurchaseOrderStatusId', 'IX_InboundOrder_InboundOrderStatusId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrder', 'IX_PurchaseOrder_SupplierId', 'IX_InboundOrder_SupplierId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrder', 'IX_PurchaseOrder_WarehouseId', 'IX_InboundOrder_WarehouseId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrderItem', 'IX_PurchaseOrderItem_PurchaseOrderId', 'IX_InboundOrderItem_InboundOrderId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('InboundOrderItem', 'IX_PurchaseOrderItem_ProductVariantId', 'IX_InboundOrderItem_ProductVariantId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('OutboundOrder', 'IX_SalesOrder_AssignedUserId', 'IX_OutboundOrder_AssignedUserId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('OutboundOrder', 'IX_SalesOrder_SalesOrderStatusId', 'IX_OutboundOrder_OutboundOrderStatusId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('OutboundOrder', 'IX_SalesOrder_WarehouseId', 'IX_OutboundOrder_WarehouseId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('OutboundOrderItem', 'IX_SalesOrderItem_SalesOrderId', 'IX_OutboundOrderItem_OutboundOrderId');");
            migrationBuilder.Sql("CALL RenameIndexIfExists('OutboundOrderItem', 'IX_SalesOrderItem_ProductVariantId', 'IX_OutboundOrderItem_ProductVariantId');");

            // 6. Clean up helper procedures
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS DropForeignKeyIfExists;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS RenameTableIfExists;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS RenameColumnIfExists;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS RenameIndexIfExists;");

            // 7. Alter column types for decimals in OutboundOrder / OutboundOrderItem (which were decimal(65,30))
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDispatchedValue",
                table: "OutboundOrder",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCostPrice",
                table: "OutboundOrderItem",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedWeightKg",
                table: "OutboundOrderItem",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualWeightKg",
                table: "OutboundOrderItem",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            // 8. Create new foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_InboundOrder_InboundOrderId",
                table: "Inventory",
                column: "InboundOrderId",
                principalTable: "InboundOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_DeliveryNote_DeliveryNoteId",
                table: "InboundOrder",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNote",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_InboundOrderStatus_InboundOrderStatusId",
                table: "InboundOrder",
                column: "InboundOrderStatusId",
                principalTable: "InboundOrderStatus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_Supplier_SupplierId",
                table: "InboundOrder",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_Warehouse_WarehouseId",
                table: "InboundOrder",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderItem_InboundOrder_InboundOrderId",
                table: "InboundOrderItem",
                column: "InboundOrderId",
                principalTable: "InboundOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderItem_ProductVariant_ProductVariantId",
                table: "InboundOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId",
                principalTable: "OutboundOrderStatus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_User_AssignedUserId",
                table: "OutboundOrder",
                column: "AssignedUserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId",
                table: "OutboundOrder",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrderItem_OutboundOrder_OutboundOrderId",
                table: "OutboundOrderItem",
                column: "OutboundOrderId",
                principalTable: "OutboundOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrderItem_ProductVariant_ProductVariantId",
                table: "OutboundOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Drop new foreign keys
            migrationBuilder.DropForeignKey(name: "FK_Inventory_InboundOrder_InboundOrderId", table: "Inventory");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrder_DeliveryNote_DeliveryNoteId", table: "InboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrder_InboundOrderStatus_InboundOrderStatusId", table: "InboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrder_Supplier_SupplierId", table: "InboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrder_Warehouse_WarehouseId", table: "InboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrderItem_InboundOrder_InboundOrderId", table: "InboundOrderItem");
            migrationBuilder.DropForeignKey(name: "FK_InboundOrderItem_ProductVariant_ProductVariantId", table: "InboundOrderItem");
            migrationBuilder.DropForeignKey(name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId", table: "OutboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_OutboundOrder_User_AssignedUserId", table: "OutboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_OutboundOrder_Warehouse_WarehouseId", table: "OutboundOrder");
            migrationBuilder.DropForeignKey(name: "FK_OutboundOrderItem_OutboundOrder_OutboundOrderId", table: "OutboundOrderItem");
            migrationBuilder.DropForeignKey(name: "FK_OutboundOrderItem_ProductVariant_ProductVariantId", table: "OutboundOrderItem");

            // 2. Rename columns back
            migrationBuilder.RenameColumn(name: "TotalAssetValue", table: "InboundOrder", newName: "TotalAmount");
            migrationBuilder.RenameColumn(name: "TotalDispatchedValue", table: "OutboundOrder", newName: "TotalAmount");
            migrationBuilder.RenameColumn(name: "InboundOrderId", table: "InboundOrderItem", newName: "PurchaseOrderId");
            migrationBuilder.RenameColumn(name: "OutboundOrderId", table: "OutboundOrderItem", newName: "SalesOrderId");
            migrationBuilder.RenameColumn(name: "InboundOrderId", table: "Inventory", newName: "PurchaseOrderId");
            migrationBuilder.RenameColumn(name: "InboundOrderId", table: "DeliveryNote", newName: "PurchaseOrderId");
            migrationBuilder.RenameColumn(name: "InboundOrderStatusId", table: "InboundOrder", newName: "PurchaseOrderStatusId");
            migrationBuilder.RenameColumn(name: "OutboundOrderStatusId", table: "OutboundOrder", newName: "SalesOrderStatusId");

            // 3. Rename tables back
            migrationBuilder.RenameTable(name: "InboundOrder", newName: "PurchaseOrder");
            migrationBuilder.RenameTable(name: "InboundOrderItem", newName: "PurchaseOrderItem");
            migrationBuilder.RenameTable(name: "InboundOrderStatus", newName: "PurchaseOrderStatus");
            migrationBuilder.RenameTable(name: "OutboundOrder", newName: "SalesOrder");
            migrationBuilder.RenameTable(name: "OutboundOrderItem", newName: "SalesOrderItem");
            migrationBuilder.RenameTable(name: "OutboundOrderStatus", newName: "SalesOrderStatus");

            // 4. Alter column types back
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "SalesOrder",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCostPrice",
                table: "SalesOrderItem",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedWeightKg",
                table: "SalesOrderItem",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualWeightKg",
                table: "SalesOrderItem",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            // 5. Rename indexes back
            migrationBuilder.RenameIndex(
                name: "IX_Inventory_InboundOrderId",
                table: "Inventory",
                newName: "IX_Inventory_PurchaseOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrder_DeliveryNoteId",
                table: "PurchaseOrder",
                newName: "IX_PurchaseOrder_DeliveryNoteId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrder_InboundOrderStatusId",
                table: "PurchaseOrder",
                newName: "IX_PurchaseOrder_PurchaseOrderStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrder_SupplierId",
                table: "PurchaseOrder",
                newName: "IX_PurchaseOrder_SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrder_WarehouseId",
                table: "PurchaseOrder",
                newName: "IX_PurchaseOrder_WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderItem_InboundOrderId",
                table: "PurchaseOrderItem",
                newName: "IX_PurchaseOrderItem_PurchaseOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_InboundOrderItem_ProductVariantId",
                table: "PurchaseOrderItem",
                newName: "IX_PurchaseOrderItem_ProductVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboundOrder_AssignedUserId",
                table: "SalesOrder",
                newName: "IX_SalesOrder_AssignedUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboundOrder_OutboundOrderStatusId",
                table: "SalesOrder",
                newName: "IX_SalesOrder_SalesOrderStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboundOrder_WarehouseId",
                table: "SalesOrder",
                newName: "IX_SalesOrder_WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboundOrderItem_OutboundOrderId",
                table: "SalesOrderItem",
                newName: "IX_SalesOrderItem_SalesOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboundOrderItem_ProductVariantId",
                table: "SalesOrderItem",
                newName: "IX_SalesOrderItem_ProductVariantId");

            // 6. Recreate old foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_PurchaseOrder_PurchaseOrderId",
                table: "Inventory",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_DeliveryNote_DeliveryNoteId",
                table: "PurchaseOrder",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNote",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_PurchaseOrderStatus_PurchaseOrderStatusId",
                table: "PurchaseOrder",
                column: "PurchaseOrderStatusId",
                principalTable: "PurchaseOrderStatus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_Supplier_SupplierId",
                table: "PurchaseOrder",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_Warehouse_WarehouseId",
                table: "PurchaseOrder",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItem_ProductVariant_ProductVariantId",
                table: "PurchaseOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItem_PurchaseOrder_PurchaseOrderId",
                table: "PurchaseOrderItem",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrder_SalesOrderStatus_SalesOrderStatusId",
                table: "SalesOrder",
                column: "SalesOrderStatusId",
                principalTable: "SalesOrderStatus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrder_User_AssignedUserId",
                table: "SalesOrder",
                column: "AssignedUserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrder_Warehouse_WarehouseId",
                table: "SalesOrder",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderItem_ProductVariant_ProductVariantId",
                table: "SalesOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderItem_SalesOrder_SalesOrderId",
                table: "SalesOrderItem",
                column: "SalesOrderId",
                principalTable: "SalesOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rice_v2_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tắt strict mode tạm thời để AlterColumn không bị lỗi với dữ liệu '0000-00-00' cũ
            migrationBuilder.Sql("SET SESSION sql_mode = '';");

            // Drop FK idempotent (MySQL does not support DROP IF EXISTS on FK)
            migrationBuilder.Sql(@"SET @v=(SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrderItem' AND CONSTRAINT_NAME='FK_InboundOrderItem_ProductVariant_ProductVariantId' AND CONSTRAINT_TYPE='FOREIGN KEY'); SET @s=IF(@v IS NOT NULL,CONCAT('ALTER TABLE `InboundOrderItem` DROP FOREIGN KEY `',@v,'`'),'SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND CONSTRAINT_NAME='FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId' AND CONSTRAINT_TYPE='FOREIGN KEY'); SET @s=IF(@v IS NOT NULL,CONCAT('ALTER TABLE `OutboundOrder` DROP FOREIGN KEY `',@v,'`'),'SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND CONSTRAINT_NAME='FK_OutboundOrder_User_AssignedUserId' AND CONSTRAINT_TYPE='FOREIGN KEY'); SET @s=IF(@v IS NOT NULL,CONCAT('ALTER TABLE `OutboundOrder` DROP FOREIGN KEY `',@v,'`'),'SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND CONSTRAINT_NAME='FK_OutboundOrder_Warehouse_WarehouseId' AND CONSTRAINT_TYPE='FOREIGN KEY'); SET @s=IF(@v IS NOT NULL,CONCAT('ALTER TABLE `OutboundOrder` DROP FOREIGN KEY `',@v,'`'),'SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrderItem' AND CONSTRAINT_NAME='FK_OutboundOrderItem_ProductVariant_ProductVariantId' AND CONSTRAINT_TYPE='FOREIGN KEY'); SET @s=IF(@v IS NOT NULL,CONCAT('ALTER TABLE `OutboundOrderItem` DROP FOREIGN KEY `',@v,'`'),'SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            // Drop Indexes idempotent
            migrationBuilder.Sql(@"SET @v=(SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND INDEX_NAME='UX_OutboundOrder_SOCode'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `OutboundOrder` DROP INDEX `UX_OutboundOrder_SOCode`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Inventory' AND INDEX_NAME='UX_Inventory_ProductVariant_Warehouse_Location'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `Inventory` DROP INDEX `UX_Inventory_ProductVariant_Warehouse_Location`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrder' AND INDEX_NAME='UX_InboundOrder_POCode'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `InboundOrder` DROP INDEX `UX_InboundOrder_POCode`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            // Drop Columns idempotent
            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='CustomerAddress'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `OutboundOrder` DROP COLUMN `CustomerAddress`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");
            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='CustomerName'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `OutboundOrder` DROP COLUMN `CustomerName`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");
            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='CustomerPhone'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `OutboundOrder` DROP COLUMN `CustomerPhone`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");
            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='SOCode'); SET @s=IF(@v IS NOT NULL,'ALTER TABLE `OutboundOrder` DROP COLUMN `SOCode`','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<decimal>(
                name: "SystemQuantity",
                table: "StockTakeItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualQuantity",
                table: "StockTakeItem",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='ProductVariant' AND COLUMN_NAME='IsByproduct'); SET @s=IF(@v IS NULL,'ALTER TABLE `ProductVariant` ADD COLUMN `IsByproduct` tinyint(1) NOT NULL DEFAULT 0','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='ProductVariant' AND COLUMN_NAME='RiceVarietyId'); SET @s=IF(@v IS NULL,'ALTER TABLE `ProductVariant` ADD COLUMN `RiceVarietyId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityPicked",
                table: "OutboundOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOrdered",
                table: "OutboundOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrderItem' AND COLUMN_NAME='SalesOrderItemId'); SET @s=IF(@v IS NULL,'ALTER TABLE `OutboundOrderItem` ADD COLUMN `SalesOrderItemId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "OutboundOrder",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='OrganizationId'); SET @s=IF(@v IS NULL,'ALTER TABLE `OutboundOrder` ADD COLUMN `OrganizationId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='OutboundOrderStatusId1'); SET @s=IF(@v IS NULL,'ALTER TABLE `OutboundOrder` ADD COLUMN `OutboundOrderStatusId1` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='SalesOrderId'); SET @s=IF(@v IS NULL,'ALTER TABLE `OutboundOrder` ADD COLUMN `SalesOrderId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='OutboundOrder' AND COLUMN_NAME='WarehouseId1'); SET @s=IF(@v IS NULL,'ALTER TABLE `OutboundOrder` ADD COLUMN `WarehouseId1` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryTransaction",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "BeforeQuantity",
                table: "InventoryTransaction",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "AfterQuantity",
                table: "InventoryTransaction",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InventoryTransaction' AND COLUMN_NAME='PaddyLotId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InventoryTransaction` ADD COLUMN `PaddyLotId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityReserved",
                table: "Inventory",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOnHand",
                table: "Inventory",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Inventory' AND COLUMN_NAME='PaddyLotId'); SET @s=IF(@v IS NULL,'ALTER TABLE `Inventory` ADD COLUMN `PaddyLotId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityReceived",
                table: "InboundOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOrdered",
                table: "InboundOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrderItem' AND COLUMN_NAME='PaddyLotId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InboundOrderItem` ADD COLUMN `PaddyLotId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrderItem' AND COLUMN_NAME='PurchaseOrderItemId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InboundOrderItem` ADD COLUMN `PurchaseOrderItemId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.AlterColumn<string>(
                name: "POCode",
                table: "InboundOrder",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrder' AND COLUMN_NAME='OrganizationId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InboundOrder` ADD COLUMN `OrganizationId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrder' AND COLUMN_NAME='PaddyPurchaseReceiptId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InboundOrder` ADD COLUMN `PaddyPurchaseReceiptId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='InboundOrder' AND COLUMN_NAME='PurchaseOrderId'); SET @s=IF(@v IS NULL,'ALTER TABLE `InboundOrder` ADD COLUMN `PurchaseOrderId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.Sql(@"SET @v=(SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerReturnOrder' AND COLUMN_NAME='CustomerId'); SET @s=IF(@v IS NULL,'ALTER TABLE `CustomerReturnOrder` ADD COLUMN `CustomerId` int NULL','SELECT 1'); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;");

            migrationBuilder.CreateTable(
                name: "LotStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSellable = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MillingOrderStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MillingOrderStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Organization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaxCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactEmail = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogoFileId = table.Column<int>(type: "int", nullable: true),
                    SubscriptionPlan = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubscriptionExpiry = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organization_FileUpload_LogoFileId",
                        column: x => x.LogoFileId,
                        principalTable: "FileUpload",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyPurchaseScheduleStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyPurchaseScheduleStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PurchaseOrderStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalesOrderStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StockTransferStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPerson = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaxCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customer_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Farmer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReputationNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farmer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Farmer_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PartyDebt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    PartyType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyDebt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyDebt_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RiceVariety",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Season = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultYieldRate = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiceVariety", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiceVariety_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PurchaseOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    POCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_PurchaseOrderStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "PurchaseOrderStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StockTransfer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    TransferCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    FromWarehouseId = table.Column<int>(type: "int", nullable: false),
                    ToWarehouseId = table.Column<int>(type: "int", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfer_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfer_StockTransferStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StockTransferStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfer_User_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransfer_Warehouse_FromWarehouseId",
                        column: x => x.FromWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfer_Warehouse_ToWarehouseId",
                        column: x => x.ToWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalesOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    SOCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RequiresMilling = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ShippingAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrder_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrder_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrder_SalesOrderStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "SalesOrderStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrder_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DebtTransaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PartyDebtId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RefId = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebtTransaction_PartyDebt_PartyDebtId",
                        column: x => x.PartyDebtId,
                        principalTable: "PartyDebt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MillingYieldConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    RiceVarietyId = table.Column<int>(type: "int", nullable: true),
                    MoistureFrom = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MoistureTo = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    YieldRate = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    BrokenRiceRate = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    BranRate = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    HuskRate = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MillingYieldConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MillingYieldConfig_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MillingYieldConfig_RiceVariety_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVariety",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyPurchaseSchedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    ScheduleCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FarmerId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    RiceVarietyId = table.Column<int>(type: "int", nullable: true),
                    ScheduleDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstimatedQtyKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ExpectedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyPurchaseSchedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseSchedule_Farmer_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "Farmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseSchedule_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseSchedule_PaddyPurchaseScheduleStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "PaddyPurchaseScheduleStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseSchedule_RiceVariety_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVariety",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseSchedule_User_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItem_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItem_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MillingOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    MillingCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SalesOrderId = table.Column<int>(type: "int", nullable: true),
                    YieldRateUsed = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    TotalRiceOutputKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ComputedPaddyKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ByproductKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    LossKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    MachineRef = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperatorId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MillingOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MillingOrder_MillingOrderStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "MillingOrderStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MillingOrder_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MillingOrder_SalesOrder_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MillingOrder_User_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MillingOrder_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalesOrderItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitSalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderItem_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderItem_SalesOrder_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyPurchaseReceipt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    ReceiptCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScheduleId = table.Column<int>(type: "int", nullable: true),
                    FarmerId = table.Column<int>(type: "int", nullable: false),
                    RiceVarietyId = table.Column<int>(type: "int", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ActualWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BagCount = table.Column<int>(type: "int", nullable: true),
                    AgreedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebtAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QualityJson = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceAdjustReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceiptDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IotWeightLogId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyPurchaseReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_Farmer_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "Farmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_IotWeightLog_IotWeightLogId",
                        column: x => x.IotWeightLogId,
                        principalTable: "IotWeightLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_PaddyPurchaseSchedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "PaddyPurchaseSchedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_RiceVariety_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVariety",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyPurchaseReceipt_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyLot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    LotCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LotType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    RiceVarietyId = table.Column<int>(type: "int", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    SourceReceiptId = table.Column<int>(type: "int", nullable: true),
                    SourceMillingOrderId = table.Column<int>(type: "int", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    InboundDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InitialWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RemainingWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CostPricePerKg = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QualityStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyLot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddyLot_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyLot_LotStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "LotStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyLot_MillingOrder_SourceMillingOrderId",
                        column: x => x.SourceMillingOrderId,
                        principalTable: "MillingOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyLot_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyLot_PaddyPurchaseReceipt_SourceReceiptId",
                        column: x => x.SourceReceiptId,
                        principalTable: "PaddyPurchaseReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyLot_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaddyLot_RiceVariety_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVariety",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyLot_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MillingOrderInput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MillingOrderId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    ReservedWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ConsumedWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MillingOrderInput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MillingOrderInput_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MillingOrderInput_MillingOrder_MillingOrderId",
                        column: x => x.MillingOrderId,
                        principalTable: "MillingOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MillingOrderInput_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MillingOrderOutput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MillingOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    OutputLotId = table.Column<int>(type: "int", nullable: true),
                    OutputType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BagCount = table.Column<int>(type: "int", nullable: true),
                    OutputWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsByproduct = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MillingOrderOutput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MillingOrderOutput_MillingOrder_MillingOrderId",
                        column: x => x.MillingOrderId,
                        principalTable: "MillingOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MillingOrderOutput_PaddyLot_OutputLotId",
                        column: x => x.OutputLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MillingOrderOutput_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "QualityInspection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PaddyLotId = table.Column<int>(type: "int", nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MoisturePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ImpurityPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MoldLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PestLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackagingStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Handling = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InspectorId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityInspection_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualityInspection_User_InspectorId",
                        column: x => x.InspectorId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StockTransferItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StockTransferId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotId = table.Column<int>(type: "int", nullable: true),
                    FromLocationId = table.Column<int>(type: "int", nullable: true),
                    ToLocationId = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferItem_Location_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransferItem_Location_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransferItem_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransferItem_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItem_StockTransfer_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "StockTransfer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "LotStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[] { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ nhập", null });

            migrationBuilder.InsertData(
                table: "LotStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "IsSellable", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[] { 2, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, true, null, "Đang lưu kho", null });

            migrationBuilder.InsertData(
                table: "LotStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 3, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ xử lý", null },
                    { 4, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Cách ly", null },
                    { 5, "#8B5CF6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang xay", null },
                    { 6, "#9CA3AF", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã dùng hết", null }
                });

            migrationBuilder.InsertData(
                table: "MillingOrderStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Nháp", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã giữ lúa", null },
                    { 3, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang xay", null },
                    { 4, "#8B5CF6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ nhập thành phẩm", null },
                    { 5, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hoàn tất", null },
                    { 6, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hủy", null }
                });

            migrationBuilder.InsertData(
                table: "Organization",
                columns: new[] { "Id", "Address", "Code", "ContactEmail", "ContactPhone", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "LastModifiedDate", "LogoFileId", "Name", "SubscriptionExpiry", "SubscriptionPlan", "TaxCode", "UpdatedBy" },
                values: new object[] { 1, null, "TUANMAY", null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hộ kinh doanh kiêm xay xát lúa gạo", true, false, null, null, "Cơ sở kinh doanh lúa gạo Tuấn Mây", null, null, null, null });

            migrationBuilder.InsertData(
                table: "PaddyPurchaseScheduleStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Mới tạo", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã xác nhận", null },
                    { 3, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang đi thu", null },
                    { 4, "#8B5CF6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã cân hàng", null },
                    { 5, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã nhập kho", null },
                    { 6, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hủy", null }
                });

            migrationBuilder.InsertData(
                table: "PurchaseOrderStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Draft", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Confirmed", null },
                    { 3, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "PartiallyReceived", null },
                    { 4, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Received", null },
                    { 5, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Cancelled", null }
                });

            migrationBuilder.InsertData(
                table: "SalesOrderStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Mới tạo", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ xác nhận", null },
                    { 3, "#8B5CF6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã giữ hàng", null },
                    { 4, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ xay", null },
                    { 5, "#06B6D4", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang chuẩn bị", null },
                    { 6, "#F97316", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang giao", null },
                    { 7, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hoàn tất", null },
                    { 8, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hủy", null }
                });

            migrationBuilder.InsertData(
                table: "StockTransferStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Nháp", null },
                    { 2, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đang chuyển", null },
                    { 3, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hoàn tất", null },
                    { 4, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hủy", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariant_RiceVarietyId",
                table: "ProductVariant",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderItem_SalesOrderItemId",
                table: "OutboundOrderItem",
                column: "SalesOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_OrganizationId",
                table: "OutboundOrder",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_OutboundOrderStatusId1",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId1");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_SalesOrderId",
                table: "OutboundOrder",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrder_WarehouseId1",
                table: "OutboundOrder",
                column: "WarehouseId1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_PaddyLotId",
                table: "InventoryTransaction",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_PaddyLotId",
                table: "Inventory",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "UX_Inventory_ProductVariant_Warehouse_Location_Lot",
                table: "Inventory",
                columns: new[] { "ProductVariantId", "WarehouseId", "LocationId", "PaddyLotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderItem_PaddyLotId",
                table: "InboundOrderItem",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderItem_PurchaseOrderItemId",
                table: "InboundOrderItem",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrder_OrganizationId",
                table: "InboundOrder",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrder_PaddyPurchaseReceiptId",
                table: "InboundOrder",
                column: "PaddyPurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrder_POCode",
                table: "InboundOrder",
                column: "POCode");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrder_PurchaseOrderId",
                table: "InboundOrder",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_CustomerId",
                table: "CustomerReturnOrder",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "UX_Customer_OrgId_Code",
                table: "Customer",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtTransaction_PartyDebtId",
                table: "DebtTransaction",
                column: "PartyDebtId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtTransaction_RefType_RefId",
                table: "DebtTransaction",
                columns: new[] { "RefType", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebtTransaction_TransactionDate",
                table: "DebtTransaction",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "UX_Farmer_OrgId_Code",
                table: "Farmer",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_OperatorId",
                table: "MillingOrder",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_SalesOrderId",
                table: "MillingOrder",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_StartedAt",
                table: "MillingOrder",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_StatusId",
                table: "MillingOrder",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrder_WarehouseId",
                table: "MillingOrder",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_MillingOrder_OrgId_Code",
                table: "MillingOrder",
                columns: new[] { "OrganizationId", "MillingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderInput_LocationId",
                table: "MillingOrderInput",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderInput_MillingOrderId",
                table: "MillingOrderInput",
                column: "MillingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderInput_PaddyLotId",
                table: "MillingOrderInput",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderOutput_MillingOrderId",
                table: "MillingOrderOutput",
                column: "MillingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderOutput_OutputLotId",
                table: "MillingOrderOutput",
                column: "OutputLotId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderOutput_ProductVariantId",
                table: "MillingOrderOutput",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MillingYieldConfig_OrgId_VarietyId_Active",
                table: "MillingYieldConfig",
                columns: new[] { "OrganizationId", "RiceVarietyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MillingYieldConfig_RiceVarietyId",
                table: "MillingYieldConfig",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_LogoFileId",
                table: "Organization",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "UX_Organization_Code",
                table: "Organization",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_InboundDate",
                table: "PaddyLot",
                column: "InboundDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_LocationId",
                table: "PaddyLot",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_ProductVariantId",
                table: "PaddyLot",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_RiceVarietyId",
                table: "PaddyLot",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_SourceMillingOrderId",
                table: "PaddyLot",
                column: "SourceMillingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_SourceReceiptId",
                table: "PaddyLot",
                column: "SourceReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_StatusId",
                table: "PaddyLot",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_WarehouseId",
                table: "PaddyLot",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_PaddyLot_OrgId_Code",
                table: "PaddyLot",
                columns: new[] { "OrganizationId", "LotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_FarmerId",
                table: "PaddyPurchaseReceipt",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_IotWeightLogId",
                table: "PaddyPurchaseReceipt",
                column: "IotWeightLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_ReceiptDate",
                table: "PaddyPurchaseReceipt",
                column: "ReceiptDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_RiceVarietyId",
                table: "PaddyPurchaseReceipt",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_ScheduleId",
                table: "PaddyPurchaseReceipt",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_WarehouseId",
                table: "PaddyPurchaseReceipt",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_PaddyPurchaseReceipt_OrgId_Code",
                table: "PaddyPurchaseReceipt",
                columns: new[] { "OrganizationId", "ReceiptCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_AssignedUserId",
                table: "PaddyPurchaseSchedule",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_FarmerId",
                table: "PaddyPurchaseSchedule",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_RiceVarietyId",
                table: "PaddyPurchaseSchedule",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_ScheduleDate",
                table: "PaddyPurchaseSchedule",
                column: "ScheduleDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_StatusId",
                table: "PaddyPurchaseSchedule",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "UX_PaddyPurchaseSchedule_OrgId_Code",
                table: "PaddyPurchaseSchedule",
                columns: new[] { "OrganizationId", "ScheduleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyDebt_OrgId_PartyType_PartyId",
                table: "PartyDebt",
                columns: new[] { "OrganizationId", "PartyType", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_OrderDate",
                table: "PurchaseOrder",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_StatusId",
                table: "PurchaseOrder",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupplierId",
                table: "PurchaseOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_WarehouseId",
                table: "PurchaseOrder",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseOrder_OrgId_POCode",
                table: "PurchaseOrder",
                columns: new[] { "OrganizationId", "POCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItem_ProductVariantId",
                table: "PurchaseOrderItem",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItem_PurchaseOrderId",
                table: "PurchaseOrderItem",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspection_InspectedAt",
                table: "QualityInspection",
                column: "InspectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspection_InspectorId",
                table: "QualityInspection",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspection_PaddyLotId",
                table: "QualityInspection",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "UX_RiceVariety_OrgId_Code",
                table: "RiceVariety",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_CustomerId",
                table: "SalesOrder",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_OrderDate",
                table: "SalesOrder",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_StatusId",
                table: "SalesOrder",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_WarehouseId",
                table: "SalesOrder",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_SalesOrder_OrgId_SOCode",
                table: "SalesOrder",
                columns: new[] { "OrganizationId", "SOCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItem_ProductVariantId",
                table: "SalesOrderItem",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItem_SalesOrderId",
                table: "SalesOrderItem",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfer_AssignedUserId",
                table: "StockTransfer",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfer_FromWarehouseId",
                table: "StockTransfer",
                column: "FromWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfer_StatusId",
                table: "StockTransfer",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfer_ToWarehouseId",
                table: "StockTransfer",
                column: "ToWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfer_TransferDate",
                table: "StockTransfer",
                column: "TransferDate");

            migrationBuilder.CreateIndex(
                name: "UX_StockTransfer_OrgId_Code",
                table: "StockTransfer",
                columns: new[] { "OrganizationId", "TransferCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItem_FromLocationId",
                table: "StockTransferItem",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItem_PaddyLotId",
                table: "StockTransferItem",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItem_ProductVariantId",
                table: "StockTransferItem",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItem_StockTransferId",
                table: "StockTransferItem",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItem_ToLocationId",
                table: "StockTransferItem",
                column: "ToLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReturnOrder_Customer_CustomerId",
                table: "CustomerReturnOrder",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_Organization_OrganizationId",
                table: "InboundOrder",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_PaddyPurchaseReceipt_PaddyPurchaseReceiptId",
                table: "InboundOrder",
                column: "PaddyPurchaseReceiptId",
                principalTable: "PaddyPurchaseReceipt",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrder_PurchaseOrder_PurchaseOrderId",
                table: "InboundOrder",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderItem_PaddyLot_PaddyLotId",
                table: "InboundOrderItem",
                column: "PaddyLotId",
                principalTable: "PaddyLot",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderItem_ProductVariant_ProductVariantId",
                table: "InboundOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundOrderItem_PurchaseOrderItem_PurchaseOrderItemId",
                table: "InboundOrderItem",
                column: "PurchaseOrderItemId",
                principalTable: "PurchaseOrderItem",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_PaddyLot_PaddyLotId",
                table: "Inventory",
                column: "PaddyLotId",
                principalTable: "PaddyLot",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_PaddyLot_PaddyLotId",
                table: "InventoryTransaction",
                column: "PaddyLotId",
                principalTable: "PaddyLot",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_Organization_OrganizationId",
                table: "OutboundOrder",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId",
                principalTable: "OutboundOrderStatus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId1",
                table: "OutboundOrder",
                column: "OutboundOrderStatusId1",
                principalTable: "OutboundOrderStatus",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_SalesOrder_SalesOrderId",
                table: "OutboundOrder",
                column: "SalesOrderId",
                principalTable: "SalesOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_User_AssignedUserId",
                table: "OutboundOrder",
                column: "AssignedUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId",
                table: "OutboundOrder",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId1",
                table: "OutboundOrder",
                column: "WarehouseId1",
                principalTable: "Warehouse",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrderItem_ProductVariant_ProductVariantId",
                table: "OutboundOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundOrderItem_SalesOrderItem_SalesOrderItemId",
                table: "OutboundOrderItem",
                column: "SalesOrderItemId",
                principalTable: "SalesOrderItem",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariant_RiceVariety_RiceVarietyId",
                table: "ProductVariant",
                column: "RiceVarietyId",
                principalTable: "RiceVariety",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReturnOrder_Customer_CustomerId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrder_Organization_OrganizationId",
                table: "InboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrder_PaddyPurchaseReceipt_PaddyPurchaseReceiptId",
                table: "InboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrder_PurchaseOrder_PurchaseOrderId",
                table: "InboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderItem_PaddyLot_PaddyLotId",
                table: "InboundOrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderItem_ProductVariant_ProductVariantId",
                table: "InboundOrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundOrderItem_PurchaseOrderItem_PurchaseOrderItemId",
                table: "InboundOrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_PaddyLot_PaddyLotId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_PaddyLot_PaddyLotId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_Organization_OrganizationId",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_OutboundOrderStatus_OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_SalesOrder_SalesOrderId",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_User_AssignedUserId",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrder_Warehouse_WarehouseId1",
                table: "OutboundOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrderItem_ProductVariant_ProductVariantId",
                table: "OutboundOrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundOrderItem_SalesOrderItem_SalesOrderItemId",
                table: "OutboundOrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariant_RiceVariety_RiceVarietyId",
                table: "ProductVariant");

            migrationBuilder.DropTable(
                name: "DebtTransaction");

            migrationBuilder.DropTable(
                name: "MillingOrderInput");

            migrationBuilder.DropTable(
                name: "MillingOrderOutput");

            migrationBuilder.DropTable(
                name: "MillingYieldConfig");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItem");

            migrationBuilder.DropTable(
                name: "QualityInspection");

            migrationBuilder.DropTable(
                name: "SalesOrderItem");

            migrationBuilder.DropTable(
                name: "StockTransferItem");

            migrationBuilder.DropTable(
                name: "PartyDebt");

            migrationBuilder.DropTable(
                name: "PurchaseOrder");

            migrationBuilder.DropTable(
                name: "PaddyLot");

            migrationBuilder.DropTable(
                name: "StockTransfer");

            migrationBuilder.DropTable(
                name: "PurchaseOrderStatus");

            migrationBuilder.DropTable(
                name: "LotStatus");

            migrationBuilder.DropTable(
                name: "MillingOrder");

            migrationBuilder.DropTable(
                name: "PaddyPurchaseReceipt");

            migrationBuilder.DropTable(
                name: "StockTransferStatus");

            migrationBuilder.DropTable(
                name: "MillingOrderStatus");

            migrationBuilder.DropTable(
                name: "SalesOrder");

            migrationBuilder.DropTable(
                name: "PaddyPurchaseSchedule");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropTable(
                name: "SalesOrderStatus");

            migrationBuilder.DropTable(
                name: "Farmer");

            migrationBuilder.DropTable(
                name: "PaddyPurchaseScheduleStatus");

            migrationBuilder.DropTable(
                name: "RiceVariety");

            migrationBuilder.DropTable(
                name: "Organization");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariant_RiceVarietyId",
                table: "ProductVariant");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrderItem_SalesOrderItemId",
                table: "OutboundOrderItem");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_OrganizationId",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_SalesOrderId",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrder_WarehouseId1",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_PaddyLotId",
                table: "InventoryTransaction");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_PaddyLotId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "UX_Inventory_ProductVariant_Warehouse_Location_Lot",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrderItem_PaddyLotId",
                table: "InboundOrderItem");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrderItem_PurchaseOrderItemId",
                table: "InboundOrderItem");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrder_OrganizationId",
                table: "InboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrder_PaddyPurchaseReceiptId",
                table: "InboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrder_POCode",
                table: "InboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrder_PurchaseOrderId",
                table: "InboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnOrder_CustomerId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "IsByproduct",
                table: "ProductVariant");

            migrationBuilder.DropColumn(
                name: "RiceVarietyId",
                table: "ProductVariant");

            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "OutboundOrderItem");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "OutboundOrderStatusId1",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "WarehouseId1",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "PaddyLotId",
                table: "InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "PaddyLotId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "PaddyLotId",
                table: "InboundOrderItem");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderItemId",
                table: "InboundOrderItem");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InboundOrder");

            migrationBuilder.DropColumn(
                name: "PaddyPurchaseReceiptId",
                table: "InboundOrder");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "InboundOrder");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CustomerReturnOrder");

            migrationBuilder.AlterColumn<int>(
                name: "SystemQuantity",
                table: "StockTakeItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "ActualQuantity",
                table: "StockTakeItem",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "QuantityPicked",
                table: "OutboundOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityOrdered",
                table: "OutboundOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "OutboundOrder",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "OutboundOrder",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "OutboundOrder",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "OutboundOrder",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SOCode",
                table: "OutboundOrder",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "InventoryTransaction",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "BeforeQuantity",
                table: "InventoryTransaction",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "AfterQuantity",
                table: "InventoryTransaction",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityReserved",
                table: "Inventory",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "QuantityOnHand",
                table: "Inventory",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityReceived",
                table: "InboundOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityOrdered",
                table: "InboundOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.UpdateData(
                table: "InboundOrder",
                keyColumn: "POCode",
                keyValue: null,
                column: "POCode",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "POCode",
                table: "InboundOrder",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_OutboundOrder_SOCode",
                table: "OutboundOrder",
                column: "SOCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Inventory_ProductVariant_Warehouse_Location",
                table: "Inventory",
                columns: new[] { "ProductVariantId", "WarehouseId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_InboundOrder_POCode",
                table: "InboundOrder",
                column: "POCode",
                unique: true);

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
                name: "FK_OutboundOrderItem_ProductVariant_ProductVariantId",
                table: "OutboundOrderItem",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaWithReviewedDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_ProductCategory_ProductCategory_ParentCategoryId",
            //     table: "ProductCategory");

            // migrationBuilder.DropColumn(
            //     name: "ApprovedBy",
            //     table: "StockTake");

            // migrationBuilder.DropColumn(
            //     name: "ParentId",
            //     table: "ProductCategory");

            // migrationBuilder.DropColumn(
            //     name: "InboundOrderId",
            //     table: "DeliveryNote");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Supplier",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Supplier",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Supplier",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Supplier",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                table: "Supplier",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Supplier",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "STCode",
                table: "StockTake",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "ProductVariant",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "QRCode",
                table: "ProductVariant",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // migrationBuilder.AddColumn<bool>(
            //     name: "IsIoTRequired",
            //     table: "ProductVariant",
            //     type: "tinyint(1)",
            //     nullable: false,
            //     defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "SOCode",
                table: "OutboundOrder",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ZoneName",
                table: "Location",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SlotCode",
                table: "Location",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ShelfRow",
                table: "Location",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ShelfLevel",
                table: "Location",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Location",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // migrationBuilder.AddColumn<int>(
            //     name: "AllowedCategoryId",
            //     table: "Location",
            //     type: "int",
            //     nullable: true);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "CreatedBy",
            //     table: "Location",
            //     type: "int",
            //     nullable: true);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "CurrentOccupancy",
            //     table: "Location",
            //     type: "int",
            //     nullable: false,
            //     defaultValue: 0);
            // 
            // migrationBuilder.AddColumn<bool>(
            //     name: "IsQuarantine",
            //     table: "Location",
            //     type: "tinyint(1)",
            //     nullable: false,
            //     defaultValue: false);
            // 
            // migrationBuilder.AddColumn<DateTime>(
            //     name: "LastModifiedDate",
            //     table: "Location",
            //     type: "datetime(6)",
            //     nullable: true);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "Priority",
            //     table: "Location",
            //     type: "int",
            //     nullable: false,
            //     defaultValue: 0);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "UpdatedBy",
            //     table: "Location",
            //     type: "int",
            //     nullable: true);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "CreatedBy",
            //     table: "Inventory",
            //     type: "int",
            //     nullable: true);
            // 
            // migrationBuilder.AddColumn<int>(
            //     name: "UpdatedBy",
            //     table: "Inventory",
            //     type: "int",
            //     nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "POCode",
                table: "InboundOrder",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingCode",
                table: "DeliveryNote",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderPhone",
                table: "DeliveryNote",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "DeliveryNote",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderAddress",
                table: "DeliveryNote",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverPhone",
                table: "DeliveryNote",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverName",
                table: "DeliveryNote",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverAddress",
                table: "DeliveryNote",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "DeclaredWeight",
                table: "DeliveryNote",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CarrierName",
                table: "DeliveryNote",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "CODAmount",
                table: "DeliveryNote",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Alert",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AlertType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedEntityType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AcknowledgedBy = table.Column<int>(type: "int", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alert_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alert_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alert_User_AcknowledgedBy",
                        column: x => x.AcknowledgedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alert_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerReturnOrderStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnOrderStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReturnToSupplierOrderStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnToSupplierOrderStatus", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerReturnOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    CustomerReturnOrderStatusId = table.Column<int>(type: "int", nullable: false),
                    OutboundOrderId = table.Column<int>(type: "int", nullable: true),
                    ReturnCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReturnReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrder_CustomerReturnOrderStatus_CustomerReturn~",
                        column: x => x.CustomerReturnOrderStatusId,
                        principalTable: "CustomerReturnOrderStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrder_OutboundOrder_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalTable: "OutboundOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrder_User_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrder_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReturnToSupplierOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ReturnToSupplierOrderStatusId = table.Column<int>(type: "int", nullable: false),
                    InboundOrderId = table.Column<int>(type: "int", nullable: true),
                    ReturnCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnToSupplierOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrder_InboundOrder_InboundOrderId",
                        column: x => x.InboundOrderId,
                        principalTable: "InboundOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrder_ReturnToSupplierOrderStatus_ReturnToSu~",
                        column: x => x.ReturnToSupplierOrderStatusId,
                        principalTable: "ReturnToSupplierOrderStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrder_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrder_User_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrder_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerReturnOrderItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerReturnOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    QuantityReturned = table.Column<int>(type: "int", nullable: false),
                    QuantityGood = table.Column<int>(type: "int", nullable: false),
                    QuantityDamaged = table.Column<int>(type: "int", nullable: false),
                    QualityStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DamageReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RestockLocationId = table.Column<int>(type: "int", nullable: true),
                    QuarantineLocationId = table.Column<int>(type: "int", nullable: true),
                    QRScanned = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_CustomerReturnOrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItem_CustomerReturnOrder_CustomerReturnOr~",
                        column: x => x.CustomerReturnOrderId,
                        principalTable: "CustomerReturnOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItem_Location_QuarantineLocationId",
                        column: x => x.QuarantineLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItem_Location_RestockLocationId",
                        column: x => x.RestockLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItem_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReturnToSupplierOrderItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReturnToSupplierOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    QuarantineLocationId = table.Column<int>(type: "int", nullable: true),
                    QuantityToReturn = table.Column<int>(type: "int", nullable: false),
                    QuantityActualReturned = table.Column<int>(type: "int", nullable: false),
                    DamageReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QRScanned = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_ReturnToSupplierOrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrderItem_Location_QuarantineLocationId",
                        column: x => x.QuarantineLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrderItem_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReturnToSupplierOrderItem_ReturnToSupplierOrder_ReturnToSupp~",
                        column: x => x.ReturnToSupplierOrderId,
                        principalTable: "ReturnToSupplierOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // migrationBuilder.CreateIndex(
            //     name: "UX_Supplier_Code",
            //     table: "Supplier",
            //     column: "Code",
            //     unique: true);
            // 
            // migrationBuilder.CreateIndex(
            //     name: "UX_StockTake_STCode",
            //     table: "StockTake",
            //     column: "STCode",
            //     unique: true);
            // 
            // migrationBuilder.CreateIndex(
            //     name: "UX_ProductVariant_QRCode",
            //     table: "ProductVariant",
            //     column: "QRCode",
            //     unique: true);
            // 
            // migrationBuilder.CreateIndex(
            //     name: "UX_ProductVariant_SKU",
            //     table: "ProductVariant",
            //     column: "SKU",
            //     unique: true);
            // 
            // migrationBuilder.CreateIndex(
            //     name: "UX_OutboundOrder_SOCode",
            //     table: "OutboundOrder",
            //     column: "SOCode",
            //     unique: true);
            // 
            // migrationBuilder.CreateIndex(
            //     name: "IX_Location_AllowedCategoryId",
            //     table: "Location",
            //     column: "AllowedCategoryId");
            // 
            // migrationBuilder.CreateIndex(
            //     name: "IX_Location_IsQuarantine",
            //     table: "Location",
            //     column: "IsQuarantine");
            // 
            // migrationBuilder.CreateIndex(
            //     name: "UX_InboundOrder_POCode",
            //     table: "InboundOrder",
            //     column: "POCode",
            //     unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alert_AcknowledgedBy",
                table: "Alert",
                column: "AcknowledgedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_AlertType",
                table: "Alert",
                column: "AlertType");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_LocationId",
                table: "Alert",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_ProductVariantId",
                table: "Alert",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_WarehouseId_Status",
                table: "Alert",
                columns: new[] { "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_ApprovedBy",
                table: "CustomerReturnOrder",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_CustomerReturnOrderStatusId",
                table: "CustomerReturnOrder",
                column: "CustomerReturnOrderStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_OutboundOrderId",
                table: "CustomerReturnOrder",
                column: "OutboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_WarehouseId",
                table: "CustomerReturnOrder",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerReturnOrder_ReturnCode",
                table: "CustomerReturnOrder",
                column: "ReturnCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItem_CustomerReturnOrderId",
                table: "CustomerReturnOrderItem",
                column: "CustomerReturnOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItem_ProductVariantId",
                table: "CustomerReturnOrderItem",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItem_QuarantineLocationId",
                table: "CustomerReturnOrderItem",
                column: "QuarantineLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItem_RestockLocationId",
                table: "CustomerReturnOrderItem",
                column: "RestockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrder_ApprovedBy",
                table: "ReturnToSupplierOrder",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrder_InboundOrderId",
                table: "ReturnToSupplierOrder",
                column: "InboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrder_ReturnToSupplierOrderStatusId",
                table: "ReturnToSupplierOrder",
                column: "ReturnToSupplierOrderStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrder_SupplierId",
                table: "ReturnToSupplierOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrder_WarehouseId",
                table: "ReturnToSupplierOrder",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UX_ReturnToSupplierOrder_ReturnCode",
                table: "ReturnToSupplierOrder",
                column: "ReturnCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrderItem_ProductVariantId",
                table: "ReturnToSupplierOrderItem",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrderItem_QuarantineLocationId",
                table: "ReturnToSupplierOrderItem",
                column: "QuarantineLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrderItem_ReturnToSupplierOrderId",
                table: "ReturnToSupplierOrderItem",
                column: "ReturnToSupplierOrderId");

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Location_ProductCategory_AllowedCategoryId",
            //     table: "Location",
            //     column: "AllowedCategoryId",
            //     principalTable: "ProductCategory",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCategory_ProductCategory_ParentCategoryId",
                table: "ProductCategory",
                column: "ParentCategoryId",
                principalTable: "ProductCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Location_ProductCategory_AllowedCategoryId",
                table: "Location");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCategory_ProductCategory_ParentCategoryId",
                table: "ProductCategory");

            migrationBuilder.DropTable(
                name: "Alert");

            migrationBuilder.DropTable(
                name: "CustomerReturnOrderItem");

            migrationBuilder.DropTable(
                name: "ReturnToSupplierOrderItem");

            migrationBuilder.DropTable(
                name: "CustomerReturnOrder");

            migrationBuilder.DropTable(
                name: "ReturnToSupplierOrder");

            migrationBuilder.DropTable(
                name: "CustomerReturnOrderStatus");

            migrationBuilder.DropTable(
                name: "ReturnToSupplierOrderStatus");

            migrationBuilder.DropIndex(
                name: "UX_Supplier_Code",
                table: "Supplier");

            migrationBuilder.DropIndex(
                name: "UX_StockTake_STCode",
                table: "StockTake");

            migrationBuilder.DropIndex(
                name: "UX_ProductVariant_QRCode",
                table: "ProductVariant");

            migrationBuilder.DropIndex(
                name: "UX_ProductVariant_SKU",
                table: "ProductVariant");

            migrationBuilder.DropIndex(
                name: "UX_OutboundOrder_SOCode",
                table: "OutboundOrder");

            migrationBuilder.DropIndex(
                name: "IX_Location_AllowedCategoryId",
                table: "Location");

            migrationBuilder.DropIndex(
                name: "IX_Location_IsQuarantine",
                table: "Location");

            migrationBuilder.DropIndex(
                name: "UX_InboundOrder_POCode",
                table: "InboundOrder");

            migrationBuilder.DropColumn(
                name: "IsIoTRequired",
                table: "ProductVariant");

            migrationBuilder.DropColumn(
                name: "AllowedCategoryId",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "CurrentOccupancy",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "IsQuarantine",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "LastModifiedDate",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Inventory");

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Supplier",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Supplier",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Supplier",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Supplier",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                table: "Supplier",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Supplier",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "STCode",
                table: "StockTake",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "StockTake",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "ProductVariant",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "QRCode",
                table: "ProductVariant",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "ProductCategory",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SOCode",
                table: "OutboundOrder",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ZoneName",
                table: "Location",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SlotCode",
                table: "Location",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ShelfRow",
                table: "Location",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ShelfLevel",
                table: "Location",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Location",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "POCode",
                table: "InboundOrder",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingCode",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderPhone",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SenderAddress",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverPhone",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverName",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverAddress",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "DeclaredWeight",
                table: "DeliveryNote",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CarrierName",
                table: "DeliveryNote",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "CODAmount",
                table: "DeliveryNote",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InboundOrderId",
                table: "DeliveryNote",
                type: "int",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCategory_ProductCategory_ParentCategoryId",
                table: "ProductCategory",
                column: "ParentCategoryId",
                principalTable: "ProductCategory",
                principalColumn: "Id");
        }
    }
}

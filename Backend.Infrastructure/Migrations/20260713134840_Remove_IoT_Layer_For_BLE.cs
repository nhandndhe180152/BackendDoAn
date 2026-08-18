using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_IoT_Layer_For_BLE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransaction_IotWeightLog_IotWeightLogId",
                table: "InventoryTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_PaddyPurchaseReceipt_IotWeightLog_IotWeightLogId",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropTable(
                name: "IotDeviceCommand");

            migrationBuilder.DropTable(
                name: "IotWeightLog");

            migrationBuilder.DropTable(
                name: "IotDevice");

            migrationBuilder.DropIndex(
                name: "IX_PaddyPurchaseReceipt_IotWeightLogId",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransaction_IotWeightLogId",
                table: "InventoryTransaction");

            migrationBuilder.DropColumn(
                name: "IsIoTRequired",
                table: "ProductVariant");

            migrationBuilder.DropColumn(
                name: "IotWeightLogId",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "IotWeightLogId",
                table: "InventoryTransaction");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIoTRequired",
                table: "ProductVariant",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IotWeightLogId",
                table: "PaddyPurchaseReceipt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IotWeightLogId",
                table: "InventoryTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IotDevice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ApiKeyHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    DeviceCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsOnline = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Location = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MqttTopic = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IotDevice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IotDevice_Warehouse",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IotDeviceCommand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IoTDeviceId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    CommandCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommandType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ExecutedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Payload = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PickedUpAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ResultMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IotDeviceCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IotDeviceCommand_IotDevice",
                        column: x => x.IoTDeviceId,
                        principalTable: "IotDevice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IotDeviceCommand_RequestedByUser",
                        column: x => x.RequestedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IotWeightLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IoTDeviceId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    IsConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsStable = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    RawValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    ReferenceItemId = table.Column<int>(type: "int", nullable: true),
                    ReferenceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestIpAddress = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Unit = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "kg")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(10,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IotWeightLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IotWeightLog_IotDevice",
                        column: x => x.IoTDeviceId,
                        principalTable: "IotDevice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IotWeightLog_ProductVariant",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_IotWeightLogId",
                table: "PaddyPurchaseReceipt",
                column: "IotWeightLogId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_IotWeightLogId",
                table: "InventoryTransaction",
                column: "IotWeightLogId");

            migrationBuilder.CreateIndex(
                name: "IX_IotDevice_WarehouseId",
                table: "IotDevice",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UQ_IotDevice_DeviceCode",
                table: "IotDevice",
                column: "DeviceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IotDeviceCommand_Device_Status",
                table: "IotDeviceCommand",
                columns: new[] { "IoTDeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IotDeviceCommand_RequestedByUserId",
                table: "IotDeviceCommand",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_IotDeviceCommand_CommandCode",
                table: "IotDeviceCommand",
                column: "CommandCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IotWeightLog_Device_MeasuredAt",
                table: "IotWeightLog",
                columns: new[] { "IoTDeviceId", "MeasuredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IotWeightLog_ProductVariant",
                table: "IotWeightLog",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_IotWeightLog_Reference",
                table: "IotWeightLog",
                columns: new[] { "ReferenceType", "ReferenceId", "ReferenceItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransaction_IotWeightLog_IotWeightLogId",
                table: "InventoryTransaction",
                column: "IotWeightLogId",
                principalTable: "IotWeightLog",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PaddyPurchaseReceipt_IotWeightLog_IotWeightLogId",
                table: "PaddyPurchaseReceipt",
                column: "IotWeightLogId",
                principalTable: "IotWeightLog",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

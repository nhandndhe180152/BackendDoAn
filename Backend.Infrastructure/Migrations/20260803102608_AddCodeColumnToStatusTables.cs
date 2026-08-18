using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeColumnToStatusTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "StockTransferStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ReturnToSupplierOrderStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "PurchaseOrderStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "OutboundOrderStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "InboundOrderStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "SUBMITTED");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "APPROVED");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "REJECTED");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "RECEIVING");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: "PARTIALLY_RECEIVED");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Code",
                value: "CONFIRMED");

            migrationBuilder.UpdateData(
                table: "InboundOrderStatus",
                keyColumn: "Id",
                keyValue: 8,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "PICKING");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "PACKED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "DISPATCHED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.UpdateData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 7,
                column: "Code",
                value: "DELIVERY_FAILED");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "CONFIRMED");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "PARTIALLY_RECEIVED");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "RECEIVED");

            migrationBuilder.UpdateData(
                table: "PurchaseOrderStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.UpdateData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "APPROVED");

            migrationBuilder.UpdateData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.UpdateData(
                table: "StockTransferStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "DRAFT");

            migrationBuilder.UpdateData(
                table: "StockTransferStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "IN_TRANSIT");

            migrationBuilder.UpdateData(
                table: "StockTransferStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "COMPLETED");

            migrationBuilder.UpdateData(
                table: "StockTransferStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.Sql("UPDATE `StockTransferStatus` SET `Code` = CONCAT('ST_', Id) WHERE `Code` = '' OR `Code` IS NULL;");
            migrationBuilder.Sql("UPDATE `ReturnToSupplierOrderStatus` SET `Code` = CONCAT('RTS_', Id) WHERE `Code` = '' OR `Code` IS NULL;");
            migrationBuilder.Sql("UPDATE `PurchaseOrderStatus` SET `Code` = CONCAT('PO_', Id) WHERE `Code` = '' OR `Code` IS NULL;");
            migrationBuilder.Sql("UPDATE `OutboundOrderStatus` SET `Code` = CONCAT('OUT_', Id) WHERE `Code` = '' OR `Code` IS NULL;");
            migrationBuilder.Sql("UPDATE `InboundOrderStatus` SET `Code` = CONCAT('INB_', Id) WHERE `Code` = '' OR `Code` IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferStatus_Code",
                table: "StockTransferStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToSupplierOrderStatus_Code",
                table: "ReturnToSupplierOrderStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderStatus_Code",
                table: "PurchaseOrderStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderStatus_Code",
                table: "OutboundOrderStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderStatus_Code",
                table: "InboundOrderStatus",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockTransferStatus_Code",
                table: "StockTransferStatus");

            migrationBuilder.DropIndex(
                name: "IX_ReturnToSupplierOrderStatus_Code",
                table: "ReturnToSupplierOrderStatus");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderStatus_Code",
                table: "PurchaseOrderStatus");

            migrationBuilder.DropIndex(
                name: "IX_OutboundOrderStatus_Code",
                table: "OutboundOrderStatus");

            migrationBuilder.DropIndex(
                name: "IX_InboundOrderStatus_Code",
                table: "InboundOrderStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "StockTransferStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ReturnToSupplierOrderStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "PurchaseOrderStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "OutboundOrderStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "InboundOrderStatus");
        }
    }
}

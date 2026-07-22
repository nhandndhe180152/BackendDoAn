using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCustomerReturnOrderInventoryAndDebtFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "DebtTransaction",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "CustomerReturnOrderStatus",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityReturned",
                table: "CustomerReturnOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityGood",
                table: "CustomerReturnOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityDamaged",
                table: "CustomerReturnOrderItem",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedCreditAmount",
                table: "CustomerReturnOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "CustomerReturnOrder",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "CustomerReturnOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtReductionAmount",
                table: "CustomerReturnOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "CustomerReturnOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundPendingAmount",
                table: "CustomerReturnOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CustomerReturnOrderItemAllocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerReturnOrderItemId = table.Column<int>(type: "int", nullable: false),
                    OutboundOrderItemAllocationId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    OriginalLocationId = table.Column<int>(type: "int", nullable: true),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityGood = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityDamaged = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CreditQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RestockLocationId = table.Column<int>(type: "int", nullable: true),
                    QuarantineLocationId = table.Column<int>(type: "int", nullable: true),
                    UnitCreditPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_CustomerReturnOrderItemAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_CustomerReturnOrderItem_Cu~",
                        column: x => x.CustomerReturnOrderItemId,
                        principalTable: "CustomerReturnOrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_Location_OriginalLocationId",
                        column: x => x.OriginalLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_Location_QuarantineLocatio~",
                        column: x => x.QuarantineLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_Location_RestockLocationId",
                        column: x => x.RestockLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_OutboundOrderItemAllocatio~",
                        column: x => x.OutboundOrderItemAllocationId,
                        principalTable: "OutboundOrderItemAllocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnOrderItemAllocation_ProductVariant_ProductVari~",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "CustomerReturnOrderStatus",
                columns: new[] { "Id", "Code", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "DRAFT", "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Nháp", null },
                    { 2, "APPROVED", "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã duyệt", null },
                    { 3, "INSPECTED", "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã kiểm định", null },
                    { 4, "CONFIRMED", "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã nhận lại hàng", null },
                    { 5, "CANCELLED", "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã hủy", null }
                });

            migrationBuilder.CreateIndex(
                name: "UX_DebtTransaction_DeduplicationKey",
                table: "DebtTransaction",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CustomerReturnOrderStatus_Code",
                table: "CustomerReturnOrderStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_ConfirmedByUserId",
                table: "CustomerReturnOrder",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_OrganizationId",
                table: "CustomerReturnOrder",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnAllocation_OutboundAllocation",
                table: "CustomerReturnOrderItemAllocation",
                column: "OutboundOrderItemAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnAllocation_PaddyLot",
                table: "CustomerReturnOrderItemAllocation",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnAllocation_ReturnItem",
                table: "CustomerReturnOrderItemAllocation",
                column: "CustomerReturnOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItemAllocation_OriginalLocationId",
                table: "CustomerReturnOrderItemAllocation",
                column: "OriginalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItemAllocation_ProductVariantId",
                table: "CustomerReturnOrderItemAllocation",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItemAllocation_QuarantineLocationId",
                table: "CustomerReturnOrderItemAllocation",
                column: "QuarantineLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrderItemAllocation_RestockLocationId",
                table: "CustomerReturnOrderItemAllocation",
                column: "RestockLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReturnOrder_Organization_OrganizationId",
                table: "CustomerReturnOrder",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReturnOrder_User_ConfirmedByUserId",
                table: "CustomerReturnOrder",
                column: "ConfirmedByUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReturnOrder_Organization_OrganizationId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReturnOrder_User_ConfirmedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropTable(
                name: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropIndex(
                name: "UX_DebtTransaction_DeduplicationKey",
                table: "DebtTransaction");

            migrationBuilder.DropIndex(
                name: "UX_CustomerReturnOrderStatus_Code",
                table: "CustomerReturnOrderStatus");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnOrder_ConfirmedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnOrder_OrganizationId",
                table: "CustomerReturnOrder");

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "DebtTransaction");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "CustomerReturnOrderStatus");

            migrationBuilder.DropColumn(
                name: "ApprovedCreditAmount",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "DebtReductionAmount",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RefundPendingAmount",
                table: "CustomerReturnOrder");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityReturned",
                table: "CustomerReturnOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityGood",
                table: "CustomerReturnOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityDamaged",
                table: "CustomerReturnOrderItem",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");
        }
    }
}

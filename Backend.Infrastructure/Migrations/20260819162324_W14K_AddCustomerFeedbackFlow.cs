using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class W14K_AddCustomerFeedbackFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (ScaleDeviceRef, WeighedAt, WeighedBy, WeightCaptureMethod were added in previous W14J migration)

            migrationBuilder.AddColumn<int>(
                name: "CustomerFeedbackId",
                table: "CustomerReturnOrder",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    OutboundOrderId = table.Column<int>(type: "int", nullable: false),
                    OutboundOrderItemId = table.Column<int>(type: "int", nullable: true),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    PaddyLotBagAllocationId = table.Column<int>(type: "int", nullable: true),
                    FeedbackType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolutionStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    ResolutionNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_OutboundOrderItem_OutboundOrderItemId",
                        column: x => x.OutboundOrderItemId,
                        principalTable: "OutboundOrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_OutboundOrder_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalTable: "OutboundOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_PaddyLotBagAllocation_PaddyLotBagAllocation~",
                        column: x => x.PaddyLotBagAllocationId,
                        principalTable: "PaddyLotBagAllocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_SalesOrder_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerFeedback_User_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnOrder_CustomerFeedbackId",
                table: "CustomerReturnOrder",
                column: "CustomerFeedbackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_OutboundOrderId",
                table: "CustomerFeedback",
                column: "OutboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_OutboundOrderItemId",
                table: "CustomerFeedback",
                column: "OutboundOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_PaddyLotBagAllocationId",
                table: "CustomerFeedback",
                column: "PaddyLotBagAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_ProductVariantId",
                table: "CustomerFeedback",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_ResolvedBy",
                table: "CustomerFeedback",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_SalesOrderId",
                table: "CustomerFeedback",
                column: "SalesOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReturnOrder_CustomerFeedback_CustomerFeedbackId",
                table: "CustomerReturnOrder",
                column: "CustomerFeedbackId",
                principalTable: "CustomerFeedback",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReturnOrder_CustomerFeedback_CustomerFeedbackId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropTable(
                name: "CustomerFeedback");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnOrder_CustomerFeedbackId",
                table: "CustomerReturnOrder");

            // (ScaleDeviceRef, WeighedAt, WeighedBy, WeightCaptureMethod were removed in previous W14J migration)

            migrationBuilder.DropColumn(
                name: "CustomerFeedbackId",
                table: "CustomerReturnOrder");
        }
    }
}

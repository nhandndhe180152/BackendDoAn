using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundOrderItemAllocationAndSeedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboundOrderItemAllocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OutboundOrderItemId = table.Column<int>(type: "int", nullable: false),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityPicked = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RowVersion = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundOrderItemAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundOrderItemAllocation_Inventory_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutboundOrderItemAllocation_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutboundOrderItemAllocation_OutboundOrderItem_OutboundOrderI~",
                        column: x => x.OutboundOrderItemId,
                        principalTable: "OutboundOrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OutboundOrderItemAllocation_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "OutboundOrderStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#6B7280", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "DRAFT", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "PICKING", null },
                    { 3, "#8B5CF6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "PACKED", null },
                    { 4, "#F97316", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "DISPATCHED", null },
                    { 5, "#10B981", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "COMPLETED", null },
                    { 6, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "CANCELLED", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderItemAllocation_InventoryId",
                table: "OutboundOrderItemAllocation",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderItemAllocation_LocationId",
                table: "OutboundOrderItemAllocation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderItemAllocation_OutboundOrderItemId",
                table: "OutboundOrderItemAllocation",
                column: "OutboundOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOrderItemAllocation_PaddyLotId",
                table: "OutboundOrderItemAllocation",
                column: "PaddyLotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundOrderItemAllocation");

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "OutboundOrderStatus",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}

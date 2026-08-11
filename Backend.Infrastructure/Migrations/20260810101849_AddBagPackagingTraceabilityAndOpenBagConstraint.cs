using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBagPackagingTraceabilityAndOpenBagConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BagDetailsJson",
                table: "PaddyPurchaseReceipt",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyLotBag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    BagNo = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QrCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StackOrder = table.Column<int>(type: "int", nullable: false),
                    StandardWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    IsFull = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BagKind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenBagKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyLotBag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddyLotBag_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaddyLotBag_PaddyLot_LotId",
                        column: x => x.LotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaddyLotBagContent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BagId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddyLotBagContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddyLotBagContent_PaddyLotBag_BagId",
                        column: x => x.BagId,
                        principalTable: "PaddyLotBag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaddyLotBagContent_PaddyLot_LotId",
                        column: x => x.LotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBag_LocationId",
                table: "PaddyLotBag",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBag_LotId_BagNo",
                table: "PaddyLotBag",
                columns: new[] { "LotId", "BagNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBag_OpenBagKey",
                table: "PaddyLotBag",
                column: "OpenBagKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBag_QrCode",
                table: "PaddyLotBag",
                column: "QrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBagContent_BagId_LotId",
                table: "PaddyLotBagContent",
                columns: new[] { "BagId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLotBagContent_LotId",
                table: "PaddyLotBagContent",
                column: "LotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaddyLotBagContent");

            migrationBuilder.DropTable(
                name: "PaddyLotBag");

            migrationBuilder.DropColumn(
                name: "BagDetailsJson",
                table: "PaddyPurchaseReceipt");
        }
    }
}

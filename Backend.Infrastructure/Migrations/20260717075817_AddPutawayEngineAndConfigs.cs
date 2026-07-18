using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPutawayEngineAndConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxCapacity",
                table: "Location",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentOccupancy",
                table: "Location",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0.000m,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CurrentProductVariantId",
                table: "Location",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSingleTypeColumn",
                table: "Location",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PutawayDecision",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotId = table.Column<int>(type: "int", nullable: true),
                    ReferenceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenceId = table.Column<int>(type: "int", nullable: false),
                    RequiredWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SuggestedLocationId = table.Column<int>(type: "int", nullable: true),
                    SelectedLocationId = table.Column<int>(type: "int", nullable: false),
                    SuggestedScore = table.Column<decimal>(type: "decimal(8,6)", nullable: true),
                    IsOverride = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OverrideReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScoreDetailsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayDecision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PutawayDecision_Location_SelectedLocationId",
                        column: x => x.SelectedLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutawayDecision_Location_SuggestedLocationId",
                        column: x => x.SuggestedLocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutawayDecision_PaddyLot_PaddyLotId",
                        column: x => x.PaddyLotId,
                        principalTable: "PaddyLot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PutawayDecision_ProductVariant_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutawayDecision_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PutawayRuleConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    CapacityWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.4000m),
                    OccupancyWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.3000m),
                    CategoryWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.2000m),
                    PriorityWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.1000m),
                    SameProductScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 1.0000m),
                    EmptyColumnScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.6000m),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayRuleConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PutawayRuleConfig_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Location_CurrentProductVariantId",
                table: "Location",
                column: "CurrentProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Location_PutawayCandidate",
                table: "Location",
                columns: new[] { "WarehouseId", "IsActive", "IsDeleted", "IsQuarantine", "CurrentProductVariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PutawayDecision_PaddyLotId",
                table: "PutawayDecision",
                column: "PaddyLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayDecision_ProductVariantId",
                table: "PutawayDecision",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayDecision_SelectedLocationId",
                table: "PutawayDecision",
                column: "SelectedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayDecision_SuggestedLocationId",
                table: "PutawayDecision",
                column: "SuggestedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayDecision_WarehouseId",
                table: "PutawayDecision",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayRuleConfig_WarehouseId",
                table: "PutawayRuleConfig",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Location_ProductVariant_CurrentProductVariantId",
                table: "Location",
                column: "CurrentProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 1. Update existing priorities
            migrationBuilder.Sql("UPDATE `Location` SET `Priority` = 50 WHERE `Priority` = 0;");

            // 2. Seed default system configuration
            migrationBuilder.Sql(@"
                INSERT INTO `PutawayRuleConfig` 
                (`WarehouseId`, `CapacityWeight`, `OccupancyWeight`, `CategoryWeight`, `PriorityWeight`, `SameProductScore`, `EmptyColumnScore`, `IsActive`, `IsDeleted`, `CreatedDate`, `CreatedBy`) 
                VALUES 
                (NULL, 0.4000, 0.3000, 0.2000, 0.1000, 1.0000, 0.6000, 1, 0, UTC_TIMESTAMP(6), 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Location_ProductVariant_CurrentProductVariantId",
                table: "Location");

            migrationBuilder.DropTable(
                name: "PutawayDecision");

            migrationBuilder.DropTable(
                name: "PutawayRuleConfig");

            migrationBuilder.DropIndex(
                name: "IX_Location_CurrentProductVariantId",
                table: "Location");

            migrationBuilder.DropIndex(
                name: "IX_Location_PutawayCandidate",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "CurrentProductVariantId",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "IsSingleTypeColumn",
                table: "Location");

            migrationBuilder.AlterColumn<int>(
                name: "MaxCapacity",
                table: "Location",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentOccupancy",
                table: "Location",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldDefaultValue: 0.000m);
        }
    }
}

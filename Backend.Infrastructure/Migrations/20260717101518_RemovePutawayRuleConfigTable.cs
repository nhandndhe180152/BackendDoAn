using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePutawayRuleConfigTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PutawayRuleConfig");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PutawayRuleConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    CapacityWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.4000m),
                    CategoryWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.2000m),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EmptyColumnScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.6000m),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OccupancyWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.3000m),
                    PriorityWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.1000m),
                    SameProductScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 1.0000m),
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
                name: "IX_PutawayRuleConfig_WarehouseId",
                table: "PutawayRuleConfig",
                column: "WarehouseId");
        }
    }
}

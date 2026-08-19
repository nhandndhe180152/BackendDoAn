using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class W14H_AddPickedFieldsToBagAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PickedAt",
                table: "PaddyLotBagAllocation",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickedBy",
                table: "PaddyLotBagAllocation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickedWeightKg",
                table: "PaddyLotBagAllocation",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickedAt",
                table: "PaddyLotBagAllocation");

            migrationBuilder.DropColumn(
                name: "PickedBy",
                table: "PaddyLotBagAllocation");

            migrationBuilder.DropColumn(
                name: "PickedWeightKg",
                table: "PaddyLotBagAllocation");
        }
    }
}

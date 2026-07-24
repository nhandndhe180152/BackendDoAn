using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuarantineLotSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AffectedWeightKg",
                table: "QualityInspection",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentLotId",
                table: "PaddyLot",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyLot_ParentLotId",
                table: "PaddyLot",
                column: "ParentLotId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaddyLot_PaddyLot_ParentLotId",
                table: "PaddyLot",
                column: "ParentLotId",
                principalTable: "PaddyLot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaddyLot_PaddyLot_ParentLotId",
                table: "PaddyLot");

            migrationBuilder.DropIndex(
                name: "IX_PaddyLot_ParentLotId",
                table: "PaddyLot");

            migrationBuilder.DropColumn(
                name: "AffectedWeightKg",
                table: "QualityInspection");

            migrationBuilder.DropColumn(
                name: "ParentLotId",
                table: "PaddyLot");
        }
    }
}

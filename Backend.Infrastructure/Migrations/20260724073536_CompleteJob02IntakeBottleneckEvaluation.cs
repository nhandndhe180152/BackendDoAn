using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteJob02IntakeBottleneckEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "PaddyPurchaseSchedule",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE PaddyPurchaseSchedule
                SET WarehouseId = (SELECT Id FROM Warehouse WHERE IsActive = 1 AND IsDeleted = 0)
                WHERE (SELECT COUNT(*) FROM Warehouse WHERE IsActive = 1 AND IsDeleted = 0) = 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseSchedule_Lookup",
                table: "PaddyPurchaseSchedule",
                columns: new[] { "WarehouseId", "StatusId", "ScheduleDate", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaddyPurchaseSchedule_Warehouse_WarehouseId",
                table: "PaddyPurchaseSchedule",
                column: "WarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaddyPurchaseSchedule_Warehouse_WarehouseId",
                table: "PaddyPurchaseSchedule");

            migrationBuilder.DropIndex(
                name: "IX_PaddyPurchaseSchedule_Lookup",
                table: "PaddyPurchaseSchedule");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "PaddyPurchaseSchedule");
        }
    }
}

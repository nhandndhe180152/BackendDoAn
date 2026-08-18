using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Feature_PaddySupplyChain_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "PaddyPurchaseScheduleStatus",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "MillingOrderOutput",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "NEW");

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "CONFIRMED");

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "COLLECTING");

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "WEIGHED");

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 5,
                column: "Code",
                value: "STOCKED");

            migrationBuilder.UpdateData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 6,
                column: "Code",
                value: "CANCELLED");

            migrationBuilder.CreateIndex(
                name: "IX_MillingOrderOutput_LocationId",
                table: "MillingOrderOutput",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_MillingOrderOutput_Location_LocationId",
                table: "MillingOrderOutput",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MillingOrderOutput_Location_LocationId",
                table: "MillingOrderOutput");

            migrationBuilder.DropIndex(
                name: "IX_MillingOrderOutput_LocationId",
                table: "MillingOrderOutput");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "PaddyPurchaseScheduleStatus");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "MillingOrderOutput");
        }
    }
}

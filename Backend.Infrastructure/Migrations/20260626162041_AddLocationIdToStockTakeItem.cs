using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationIdToStockTakeItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "StockTakeItem",
                type: "int",
                nullable: true);

            migrationBuilder.InsertData(
                table: "StockTakeStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#ff9500", null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Mới tạo", null },
                    { 2, "#007bff", null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã gửi yêu cầu", null },
                    { 3, "#00b315", null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã duyệt", null },
                    { 4, "#ff0000", null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Từ chối", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItem_LocationId",
                table: "StockTakeItem",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItem_Location_LocationId",
                table: "StockTakeItem",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItem_Location_LocationId",
                table: "StockTakeItem");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeItem_LocationId",
                table: "StockTakeItem");

            migrationBuilder.DeleteData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "StockTakeItem");
        }
    }
}

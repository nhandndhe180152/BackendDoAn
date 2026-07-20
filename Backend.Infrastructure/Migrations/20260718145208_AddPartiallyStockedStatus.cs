using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartiallyStockedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PaddyPurchaseScheduleStatus",
                columns: new[] { "Id", "Code", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[] { 7, "PARTIALLY_STOCKED", "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Nhập một phần", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PaddyPurchaseScheduleStatus",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}

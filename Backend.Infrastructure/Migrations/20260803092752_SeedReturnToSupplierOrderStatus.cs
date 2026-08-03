using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedReturnToSupplierOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReturnToSupplierOrderStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "#F59E0B", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Chờ duyệt", null },
                    { 2, "#3B82F6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã duyệt", null },
                    { 3, "#16A34A", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Hoàn thành", null },
                    { 4, "#EF4444", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Đã huỷ", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ReturnToSupplierOrderStatus",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}

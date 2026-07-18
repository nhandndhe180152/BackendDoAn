using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RC2_SeedProductVariantCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ProductCategory",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "LastModifiedDate", "Name", "ParentCategoryId", "SortOrder", "TreeIds", "UpdatedBy" },
                values: new object[,]
                {
                    { 101, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lúa nguyên liệu đầu vào", false, null, "Lúa thô", null, 101, "101", null },
                    { 102, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Gạo sau xay xát", false, null, "Gạo thành phẩm", null, 102, "102", null },
                    { 103, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Tấm, cám, trấu từ quá trình xay xát", false, null, "Phụ phẩm", null, 103, "103", null }
                });

            migrationBuilder.InsertData(
                table: "UnitOfMeasure",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "IsDeleted", "LastModifiedDate", "Name", "Symbol", "UpdatedBy" },
                values: new object[,]
                {
                    { 101, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Kilogram", "kg", null },
                    { 102, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Tấn", "T", null },
                    { 103, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Bao (50kg)", "bao", null }
                });

            migrationBuilder.InsertData(
                table: "Product",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "LastModifiedDate", "Name", "ProductCategoryId", "UpdatedBy" },
                values: new object[,]
                {
                    { 101, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lúa nguyên liệu đầu vào thu mua từ nông dân", true, false, null, "Lúa thô", 101, null },
                    { 102, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Gạo thành phẩm sau xay xát", true, false, null, "Gạo", 102, null },
                    { 103, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Tấm, cám, trấu sinh ra trong quá trình xay xát", true, false, null, "Phụ phẩm", 103, null }
                });

            migrationBuilder.InsertData(
                table: "ProductVariant",
                columns: new[] { "Id", "AttributeValues", "CostPrice", "CreatedBy", "CreatedDate", "Description", "ImageId", "IsActive", "IsDeleted", "LastModifiedDate", "MinStockLevel", "Name", "ProductId", "QRCode", "RiceVarietyId", "SKU", "SalePrice", "UnitOfMeasureId", "UpdatedBy", "Weight" },
                values: new object[,]
                {
                    { 101, null, 0m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, null, "Lúa thô (chung)", 101, null, null, "PV-LUA-CHUNG", 0m, 101, null, 50m },
                    { 102, null, 0m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, false, null, null, "Gạo (chung)", 102, null, null, "PV-GAO-CHUNG", 0m, 101, null, 50m }
                });

            migrationBuilder.InsertData(
                table: "ProductVariant",
                columns: new[] { "Id", "AttributeValues", "CostPrice", "CreatedBy", "CreatedDate", "Description", "ImageId", "IsActive", "IsByproduct", "IsDeleted", "LastModifiedDate", "MinStockLevel", "Name", "ProductId", "QRCode", "RiceVarietyId", "SKU", "SalePrice", "UnitOfMeasureId", "UpdatedBy", "Weight" },
                values: new object[] { 103, null, 0m, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, true, false, null, null, "Phụ phẩm (chung)", 103, null, null, "PV-PHUPHAM-CHUNG", 0m, 101, null, 50m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ProductVariant",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "ProductVariant",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "ProductVariant",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "UnitOfMeasure",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "UnitOfMeasure",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "Product",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "Product",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "Product",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "UnitOfMeasure",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "ProductCategory",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "ProductCategory",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "ProductCategory",
                keyColumn: "Id",
                keyValue: 103);
        }
    }
}

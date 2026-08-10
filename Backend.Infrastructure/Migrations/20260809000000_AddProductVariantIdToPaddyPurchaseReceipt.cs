using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariantIdToPaddyPurchaseReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductVariantId",
                table: "PaddyPurchaseReceipt",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaddyPurchaseReceipt_ProductVariantId",
                table: "PaddyPurchaseReceipt",
                column: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaddyPurchaseReceipt_ProductVariant_ProductVariantId",
                table: "PaddyPurchaseReceipt",
                column: "ProductVariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaddyPurchaseReceipt_ProductVariant_ProductVariantId",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropIndex(
                name: "IX_PaddyPurchaseReceipt_ProductVariantId",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "PaddyPurchaseReceipt");
        }
    }
}

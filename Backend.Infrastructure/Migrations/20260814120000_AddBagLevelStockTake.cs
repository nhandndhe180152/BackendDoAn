using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pomelo.EntityFrameworkCore.MySql.Metadata;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <summary>
    /// Kiểm kê theo BAO thay vì chỉ theo kg.
    ///
    /// - StockTakeItem: thêm số bao sổ sách / số bao đếm được + tình trạng chất lượng.
    /// - StockTakeItemBag (bảng mới): ảnh chụp từng bao trong phạm vi kiểm kê và
    ///   kết quả kiểm đếm của bao đó (quét QR, cân, chất lượng).
    ///
    /// Không đụng dữ liệu cũ: phiếu đã có giữ SystemBagCount = 0 và
    /// CountedBagCount = NULL, nghĩa là "phiếu kiểm theo kg" như trước.
    /// </summary>
    public partial class AddBagLevelStockTake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SystemBagCount",
                table: "StockTakeItem",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CountedBagCount",
                table: "StockTakeItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityStatus",
                table: "StockTakeItem",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "OK")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "QualityNote",
                table: "StockTakeItem",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "QualityImageUrls",
                table: "StockTakeItem",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StockTakeItemBag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StockTakeItemId = table.Column<int>(type: "int", nullable: false),
                    PaddyLotBagId = table.Column<int>(type: "int", nullable: false),
                    BagNo = table.Column<int>(type: "int", nullable: false),
                    QrCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SystemWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CountedWeightKg = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Counted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ScannedByQr = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsUnexpected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QualityStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "OK")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CountedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeItemBag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTakeItemBag_StockTakeItem_StockTakeItemId",
                        column: x => x.StockTakeItemId,
                        principalTable: "StockTakeItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    // Restrict: xoá phiếu kiểm kê không được kéo theo bao hàng thật.
                    table.ForeignKey(
                        name: "FK_StockTakeItemBag_PaddyLotBag_PaddyLotBagId",
                        column: x => x.PaddyLotBagId,
                        principalTable: "PaddyLotBag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakeItemBag_User_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Một bao chỉ được ghi nhận một lần trong một dòng kiểm kê.
            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItemBag_StockTakeItemId_PaddyLotBagId",
                table: "StockTakeItemBag",
                columns: new[] { "StockTakeItemId", "PaddyLotBagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItemBag_PaddyLotBagId",
                table: "StockTakeItemBag",
                column: "PaddyLotBagId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItemBag_CountedByUserId",
                table: "StockTakeItemBag",
                column: "CountedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StockTakeItemBag");

            migrationBuilder.DropColumn(name: "SystemBagCount", table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "CountedBagCount", table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "QualityStatus", table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "QualityNote", table: "StockTakeItem");
            migrationBuilder.DropColumn(name: "QualityImageUrls", table: "StockTakeItem");
        }
    }
}

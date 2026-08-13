using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <summary>
    /// Lưu lại nguồn gốc khối lượng đóng gói của phiếu xuất:
    /// - OutboundOrder.PackingScaleDevice: tên cân điện tử đã dùng (null = nhập tay).
    /// - OutboundOrder.PackedDate: thời điểm chốt đóng gói.
    /// - OutboundOrderItem.ActualWeightSource: "SCALE" hoặc "MANUAL" cho từng dòng.
    /// </summary>
    public partial class AddPackingWeightSourceToOutbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackingScaleDevice",
                table: "OutboundOrder",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PackedDate",
                table: "OutboundOrder",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActualWeightSource",
                table: "OutboundOrderItem",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackingScaleDevice",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "PackedDate",
                table: "OutboundOrder");

            migrationBuilder.DropColumn(
                name: "ActualWeightSource",
                table: "OutboundOrderItem");
        }
    }
}

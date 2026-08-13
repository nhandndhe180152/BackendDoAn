using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Cho phép một bao có nhiều allocation lịch sử nhưng chỉ tối đa một allocation ACTIVE.
/// MySQL bỏ qua filter của filtered index cũ nên index BagId đã vô tình unique vĩnh viễn.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260813230000_FixActiveBagAllocationUniqueIndex")]
public partial class FixActiveBagAllocationUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // BagId đang là cột FK; MySQL không cho bỏ index duy nhất đang hỗ trợ FK.
        // Tạo index tạm trước để khóa ngoại luôn được bảo vệ trong lúc đổi index.
        migrationBuilder.CreateIndex(
            name: "IX_PaddyLotBagAllocation_BagId_FK_Temp",
            table: "PaddyLotBagAllocation",
            column: "BagId");

        migrationBuilder.DropIndex(
            name: "IX_PaddyLotBagAllocation_BagId",
            table: "PaddyLotBagAllocation");

        migrationBuilder.AddColumn<int>(
            name: "ActiveBagId",
            table: "PaddyLotBagAllocation",
            type: "int",
            nullable: true,
            computedColumnSql: "CASE WHEN `Status` = 'ACTIVE' AND `IsDeleted` = 0 THEN `BagId` ELSE NULL END",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaddyLotBagAllocation_BagId",
            table: "PaddyLotBagAllocation",
            column: "BagId");

        migrationBuilder.CreateIndex(
            name: "IX_PaddyLotBagAllocation_ActiveBagId",
            table: "PaddyLotBagAllocation",
            column: "ActiveBagId",
            unique: true);

        migrationBuilder.DropIndex(
            name: "IX_PaddyLotBagAllocation_BagId_FK_Temp",
            table: "PaddyLotBagAllocation");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PaddyLotBagAllocation_ActiveBagId",
            table: "PaddyLotBagAllocation");

        migrationBuilder.DropIndex(
            name: "IX_PaddyLotBagAllocation_BagId",
            table: "PaddyLotBagAllocation");

        migrationBuilder.DropColumn(
            name: "ActiveBagId",
            table: "PaddyLotBagAllocation");

        // Down chỉ thành công nếu dữ liệu không có nhiều allocation lịch sử cho cùng một bao.
        migrationBuilder.CreateIndex(
            name: "IX_PaddyLotBagAllocation_BagId",
            table: "PaddyLotBagAllocation",
            column: "BagId",
            unique: true);
    }
}

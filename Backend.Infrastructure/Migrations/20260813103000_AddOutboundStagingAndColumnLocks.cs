using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

[DbContext(typeof(BackendContext))]
[Migration("20260813103000_AddOutboundStagingAndColumnLocks")]
public partial class AddOutboundStagingAndColumnLocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsOutboundStaging", table: "Location", type: "tinyint(1)",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(
            name: "OutboundLockOrderId", table: "Location", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "OutboundLockedAt", table: "Location", type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "RowVersion", table: "Location", type: "timestamp(6)",
            rowVersion: true, nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Location_OutboundLockOrderId", table: "Location", column: "OutboundLockOrderId");
        migrationBuilder.CreateIndex(
            name: "IX_Location_OutboundStaging", table: "Location",
            columns: new[] { "WarehouseId", "IsOutboundStaging", "IsActive", "IsDeleted" });
        migrationBuilder.AddForeignKey(
            name: "FK_Location_OutboundOrder_OutboundLockOrderId", table: "Location",
            column: "OutboundLockOrderId", principalTable: "OutboundOrder", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Location_OutboundOrder_OutboundLockOrderId", "Location");
        migrationBuilder.DropIndex("IX_Location_OutboundLockOrderId", "Location");
        migrationBuilder.DropIndex("IX_Location_OutboundStaging", "Location");
        migrationBuilder.DropColumn("IsOutboundStaging", "Location");
        migrationBuilder.DropColumn("OutboundLockOrderId", "Location");
        migrationBuilder.DropColumn("OutboundLockedAt", "Location");
        migrationBuilder.DropColumn("RowVersion", "Location");
    }
}

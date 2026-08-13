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
        migrationBuilder.AddColumn<int>(
            name: "SourceBagId", table: "PaddyLotBag", type: "int", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Location_OutboundLockOrderId", table: "Location", column: "OutboundLockOrderId");
        migrationBuilder.CreateIndex(
            name: "IX_Location_OutboundStaging", table: "Location",
            columns: new[] { "WarehouseId", "IsOutboundStaging", "IsActive", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "IX_PaddyLotBag_SourceBagId", table: "PaddyLotBag", column: "SourceBagId");
        migrationBuilder.AddForeignKey(
            name: "FK_Location_OutboundOrder_OutboundLockOrderId", table: "Location",
            column: "OutboundLockOrderId", principalTable: "OutboundOrder", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
        migrationBuilder.AddForeignKey(
            name: "FK_PaddyLotBag_PaddyLotBag_SourceBagId", table: "PaddyLotBag",
            column: "SourceBagId", principalTable: "PaddyLotBag", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql(@"
            INSERT INTO `Location`
                (`WarehouseId`, `ZoneName`, `ShelfRow`, `ShelfLevel`, `SlotCode`, `MaxCapacity`,
                 `Description`, `IsActive`, `CurrentOccupancy`, `AllowedCategoryId`, `Priority`,
                 `IsQuarantine`, `IsOutboundStaging`, `IsSingleTypeColumn`, `QrCode`, `QrImageUrl`,
                 `IsDeleted`, `CreatedDate`, `LastModifiedDate`, `CreatedBy`, `UpdatedBy`)
            SELECT w.`Id`, 'Khu chờ xuất', 'STAGING', NULL, CONCAT('OUT-STAGING-', w.`Id`), NULL,
                   'Vị trí hệ thống cho hàng đã đóng gói chờ xuất', 1, 0, NULL, 0,
                   0, 1, 0, CONCAT('LC-OUT-STAGING-', w.`Id`), '',
                   0, UTC_TIMESTAMP(6), NULL, NULL, NULL
            FROM `Warehouse` w
            WHERE w.`IsDeleted` = 0
              AND NOT EXISTS (
                  SELECT 1 FROM `Location` l
                  WHERE l.`WarehouseId` = w.`Id` AND l.`IsOutboundStaging` = 1 AND l.`IsDeleted` = 0
              );");
        migrationBuilder.Sql(@"
            ALTER TABLE `Location`
                ADD COLUMN `OutboundStagingWarehouseId` int
                GENERATED ALWAYS AS (
                    CASE WHEN `IsOutboundStaging` = 1 AND `IsDeleted` = 0 THEN `WarehouseId` ELSE NULL END
                ) STORED;");
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX `UX_Location_OneOutboundStagingPerWarehouse`
                ON `Location` (`OutboundStagingWarehouseId`);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Location_OutboundOrder_OutboundLockOrderId", "Location");
        migrationBuilder.DropForeignKey("FK_PaddyLotBag_PaddyLotBag_SourceBagId", "PaddyLotBag");
        migrationBuilder.DropIndex("IX_Location_OutboundLockOrderId", "Location");
        migrationBuilder.DropIndex("IX_Location_OutboundStaging", "Location");
        migrationBuilder.DropIndex("UX_Location_OneOutboundStagingPerWarehouse", "Location");
        migrationBuilder.DropIndex("IX_PaddyLotBag_SourceBagId", "PaddyLotBag");
        migrationBuilder.DropColumn("IsOutboundStaging", "Location");
        migrationBuilder.DropColumn("OutboundLockOrderId", "Location");
        migrationBuilder.DropColumn("OutboundLockedAt", "Location");
        migrationBuilder.DropColumn("OutboundStagingWarehouseId", "Location");
        migrationBuilder.DropColumn("SourceBagId", "PaddyLotBag");
    }
}

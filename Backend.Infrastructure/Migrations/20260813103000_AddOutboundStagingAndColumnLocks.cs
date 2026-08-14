using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Thêm khu chờ xuất (staging) + khóa cột cho phiếu xuất, và truy vết bao nguồn.
///
/// VÌ SAO MIGRATION NÀY VIẾT BẰNG SQL CÓ ĐIỀU KIỆN (idempotent):
/// MySQL KHÔNG rollback DDL — mỗi ALTER TABLE tự commit. Nếu migration lỗi ở câu giữa chừng,
/// các cột đã tạo vẫn nằm lại trong DB nhưng dòng ghi nhận trong `__EFMigrationsHistory`
/// KHÔNG được ghi. Lần khởi động sau EF chạy lại migration từ đầu và chết với
/// "Duplicate column name 'IsOutboundStaging'" -> container restart loop, không tự thoát ra được.
///
/// Bản này kiểm tra INFORMATION_SCHEMA trước mỗi thao tác nên chạy lại bao nhiêu lần cũng an toàn:
/// cái gì đã có thì bỏ qua, cái gì thiếu thì tạo bù -> schema luôn đầy đủ, không cần sửa tay DB.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260813103000_AddOutboundStagingAndColumnLocks")]
public partial class AddOutboundStagingAndColumnLocks : Migration
{
    private const string ProcName = "sp__mig_20260813103000";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Dùng stored procedure vì MySQL không có "ADD COLUMN IF NOT EXISTS",
        // còn PREPARE/EXECUTE với biến @ thì bị MySqlConnector hiểu nhầm là tham số truy vấn.
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");

        migrationBuilder.Sql($@"
CREATE PROCEDURE `{ProcName}`()
BEGIN
    -- ---------- Cột ----------
    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'IsOutboundStaging') = 0 THEN
        ALTER TABLE `Location` ADD COLUMN `IsOutboundStaging` tinyint(1) NOT NULL DEFAULT 0;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundLockOrderId') = 0 THEN
        ALTER TABLE `Location` ADD COLUMN `OutboundLockOrderId` int NULL;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundLockedAt') = 0 THEN
        ALTER TABLE `Location` ADD COLUMN `OutboundLockedAt` datetime(6) NULL;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBag'
           AND COLUMN_NAME = 'SourceBagId') = 0 THEN
        ALTER TABLE `PaddyLotBag` ADD COLUMN `SourceBagId` int NULL;
    END IF;

    -- ---------- Index thường ----------
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'IX_Location_OutboundLockOrderId') = 0 THEN
        CREATE INDEX `IX_Location_OutboundLockOrderId` ON `Location` (`OutboundLockOrderId`);
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'IX_Location_OutboundStaging') = 0 THEN
        CREATE INDEX `IX_Location_OutboundStaging`
            ON `Location` (`WarehouseId`, `IsOutboundStaging`, `IsActive`, `IsDeleted`);
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBag'
           AND INDEX_NAME = 'IX_PaddyLotBag_SourceBagId') = 0 THEN
        CREATE INDEX `IX_PaddyLotBag_SourceBagId` ON `PaddyLotBag` (`SourceBagId`);
    END IF;

    -- ---------- Khóa ngoại ----------
    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_Location_OutboundOrder_OutboundLockOrderId') = 0 THEN
        ALTER TABLE `Location`
            ADD CONSTRAINT `FK_Location_OutboundOrder_OutboundLockOrderId`
            FOREIGN KEY (`OutboundLockOrderId`) REFERENCES `OutboundOrder` (`Id`) ON DELETE SET NULL;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_PaddyLotBag_PaddyLotBag_SourceBagId') = 0 THEN
        ALTER TABLE `PaddyLotBag`
            ADD CONSTRAINT `FK_PaddyLotBag_PaddyLotBag_SourceBagId`
            FOREIGN KEY (`SourceBagId`) REFERENCES `PaddyLotBag` (`Id`) ON DELETE SET NULL;
    END IF;

    -- ---------- Seed vị trí 'Khu chờ xuất' cho từng kho ----------
    -- Chạy TRƯỚC khi tạo unique index để index không vướng dữ liệu nửa vời.
    INSERT INTO `Location`
        (`WarehouseId`, `ZoneName`, `ShelfRow`, `ShelfLevel`, `SlotCode`, `MaxCapacity`,
         `Description`, `IsActive`, `CurrentOccupancy`, `AllowedCategoryId`, `Priority`,
         `IsQuarantine`, `IsOutboundStaging`, `IsSingleTypeColumn`, `QrCode`, `QrImageUrl`,
         `IsDeleted`, `CreatedDate`, `LastModifiedDate`, `CreatedBy`, `UpdatedBy`)
    SELECT w.`Id`, 'Khu chờ xuất', 'STAGING', NULL, CONCAT('OUT-STAGING-', w.`Id`), NULL,
           'Vị trí hệ thống cho hàng đã đóng gói chờ xuất', 1, 0, NULL, 0,
           0, 1, 0, CONCAT('LC-OUT-STAGING-', w.`Id`), '',
           0, DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 7 HOUR), NULL, NULL, NULL
    FROM `Warehouse` w
    WHERE w.`IsDeleted` = 0
      AND NOT EXISTS (
          SELECT 1 FROM `Location` l
          WHERE l.`WarehouseId` = w.`Id` AND l.`IsOutboundStaging` = 1 AND l.`IsDeleted` = 0
      );

    -- ---------- Cột sinh + unique index (mỗi kho tối đa 1 khu chờ xuất) ----------
    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundStagingWarehouseId') = 0 THEN
        ALTER TABLE `Location`
            ADD COLUMN `OutboundStagingWarehouseId` int
            GENERATED ALWAYS AS (
                CASE WHEN `IsOutboundStaging` = 1 AND `IsDeleted` = 0 THEN `WarehouseId` ELSE NULL END
            ) STORED;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'UX_Location_OneOutboundStagingPerWarehouse') = 0 THEN
        CREATE UNIQUE INDEX `UX_Location_OneOutboundStagingPerWarehouse`
            ON `Location` (`OutboundStagingWarehouseId`);
    END IF;
END;");

        migrationBuilder.Sql($"CALL `{ProcName}`();");
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down cũng viết có điều kiện để không chết khi schema đang ở trạng thái nửa vời.
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}_down`;");

        migrationBuilder.Sql($@"
CREATE PROCEDURE `{ProcName}_down`()
BEGIN
    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_Location_OutboundOrder_OutboundLockOrderId') > 0 THEN
        ALTER TABLE `Location` DROP FOREIGN KEY `FK_Location_OutboundOrder_OutboundLockOrderId`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_PaddyLotBag_PaddyLotBag_SourceBagId') > 0 THEN
        ALTER TABLE `PaddyLotBag` DROP FOREIGN KEY `FK_PaddyLotBag_PaddyLotBag_SourceBagId`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'UX_Location_OneOutboundStagingPerWarehouse') > 0 THEN
        DROP INDEX `UX_Location_OneOutboundStagingPerWarehouse` ON `Location`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'IX_Location_OutboundStaging') > 0 THEN
        DROP INDEX `IX_Location_OutboundStaging` ON `Location`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'IX_Location_OutboundLockOrderId') > 0 THEN
        DROP INDEX `IX_Location_OutboundLockOrderId` ON `Location`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBag'
           AND INDEX_NAME = 'IX_PaddyLotBag_SourceBagId') > 0 THEN
        DROP INDEX `IX_PaddyLotBag_SourceBagId` ON `PaddyLotBag`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundStagingWarehouseId') > 0 THEN
        ALTER TABLE `Location` DROP COLUMN `OutboundStagingWarehouseId`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'IsOutboundStaging') > 0 THEN
        ALTER TABLE `Location` DROP COLUMN `IsOutboundStaging`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundLockOrderId') > 0 THEN
        ALTER TABLE `Location` DROP COLUMN `OutboundLockOrderId`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundLockedAt') > 0 THEN
        ALTER TABLE `Location` DROP COLUMN `OutboundLockedAt`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBag'
           AND COLUMN_NAME = 'SourceBagId') > 0 THEN
        ALTER TABLE `PaddyLotBag` DROP COLUMN `SourceBagId`;
    END IF;
END;");

        migrationBuilder.Sql($"CALL `{ProcName}_down`();");
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}_down`;");
    }
}

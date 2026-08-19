using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <summary>
    /// Kiểm kê theo BAO: phạm vi phiếu (khu/cột/lô, cờ cách ly), số bao + lý do lệch + chỉnh lý
    /// ở dòng kiểm kê, và bảng StockTakeItemBag ghi kết quả từng bao (đếm, cân, chất lượng, xử lý).
    /// Dùng INFORMATION_SCHEMA để chạy lại được nhiều lần trên MySQL 5.7+.
    /// </summary>
    public partial class StockTakeByBag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateStockTakeByBag`;
CREATE PROCEDURE `_MigrateStockTakeByBag`()
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'ScopeType') THEN
        ALTER TABLE `StockTake` ADD COLUMN `ScopeType` varchar(20) CHARACTER SET utf8mb4 NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'ScopeZoneName') THEN
        ALTER TABLE `StockTake` ADD COLUMN `ScopeZoneName` varchar(255) CHARACTER SET utf8mb4 NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'ScopeLocationId') THEN
        ALTER TABLE `StockTake` ADD COLUMN `ScopeLocationId` int NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'ScopePaddyLotId') THEN
        ALTER TABLE `StockTake` ADD COLUMN `ScopePaddyLotId` int NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'IsQuarantineScope') THEN
        ALTER TABLE `StockTake` ADD COLUMN `IsQuarantineScope` tinyint(1) NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND INDEX_NAME = 'IX_StockTake_ScopeLocationId') THEN
        CREATE INDEX `IX_StockTake_ScopeLocationId` ON `StockTake` (`ScopeLocationId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND INDEX_NAME = 'IX_StockTake_ScopePaddyLotId') THEN
        CREATE INDEX `IX_StockTake_ScopePaddyLotId` ON `StockTake` (`ScopePaddyLotId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake'
          AND CONSTRAINT_NAME = 'FK_StockTake_Location_ScopeLocationId') THEN
        ALTER TABLE `StockTake` ADD CONSTRAINT `FK_StockTake_Location_ScopeLocationId`
            FOREIGN KEY (`ScopeLocationId`) REFERENCES `Location` (`Id`) ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake'
          AND CONSTRAINT_NAME = 'FK_StockTake_PaddyLot_ScopePaddyLotId') THEN
        ALTER TABLE `StockTake` ADD CONSTRAINT `FK_StockTake_PaddyLot_ScopePaddyLotId`
            FOREIGN KEY (`ScopePaddyLotId`) REFERENCES `PaddyLot` (`Id`) ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'SystemBagCount') THEN
        ALTER TABLE `StockTakeItem` ADD COLUMN `SystemBagCount` int NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'CountedBagCount') THEN
        ALTER TABLE `StockTakeItem` ADD COLUMN `CountedBagCount` int NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'VarianceReason') THEN
        ALTER TABLE `StockTakeItem` ADD COLUMN `VarianceReason` varchar(500) CHARACTER SET utf8mb4 NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'AdjustedBagCount') THEN
        ALTER TABLE `StockTakeItem` ADD COLUMN `AdjustedBagCount` int NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'AdjustedWeightKg') THEN
        ALTER TABLE `StockTakeItem` ADD COLUMN `AdjustedWeightKg` decimal(18,3) NULL;
    END IF;
END;
CALL `_MigrateStockTakeByBag`();
DROP PROCEDURE IF EXISTS `_MigrateStockTakeByBag`;
");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `StockTakeItemBag` (
    `Id`               int NOT NULL AUTO_INCREMENT,
    `StockTakeItemId`  int NOT NULL,
    `PaddyLotBagId`    int NOT NULL,
    `BagNo`            int NOT NULL,
    `QrCode`           varchar(100) CHARACTER SET utf8mb4 NULL,
    `SystemWeightKg`   decimal(18,3) NOT NULL,
    `SystemStackOrder` int NOT NULL,
    `PickSequence`     int NOT NULL,
    `RestowSequence`   int NOT NULL,
    `Counted`          tinyint(1) NOT NULL DEFAULT 0,
    `ScannedByQr`      tinyint(1) NOT NULL DEFAULT 0,
    `CountedWeightKg`  decimal(18,3) NULL,
    `IsUnexpected`     tinyint(1) NOT NULL DEFAULT 0,
    `QualityResult`    varchar(30) CHARACTER SET utf8mb4 NULL,
    `MoldLevel`        varchar(50) CHARACTER SET utf8mb4 NULL,
    `PestLevel`        varchar(50) CHARACTER SET utf8mb4 NULL,
    `PackagingStatus`  varchar(50) CHARACTER SET utf8mb4 NULL,
    `MoisturePercent`  decimal(5,2) NULL,
    `ImpurityPercent`  decimal(5,2) NULL,
    `QualityNote`      varchar(1000) CHARACTER SET utf8mb4 NULL,
    `Disposition`      varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `TargetLocationId` int NULL,
    `DispositionNote`  varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsDeleted`        tinyint(1) NOT NULL DEFAULT 0,
    `CreatedDate`      datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `LastModifiedDate` datetime(6) NULL,
    `CreatedBy`        int NULL,
    `UpdatedBy`        int NULL,
    CONSTRAINT `PK_StockTakeItemBag` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StockTakeItemBag_StockTakeItem_StockTakeItemId`
        FOREIGN KEY (`StockTakeItemId`) REFERENCES `StockTakeItem` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_StockTakeItemBag_PaddyLotBag_PaddyLotBagId`
        FOREIGN KEY (`PaddyLotBagId`) REFERENCES `PaddyLotBag` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_StockTakeItemBag_Location_TargetLocationId`
        FOREIGN KEY (`TargetLocationId`) REFERENCES `Location` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;
");

            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateStockTakeItemBagIndexes`;
CREATE PROCEDURE `_MigrateStockTakeItemBagIndexes`()
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItemBag'
          AND INDEX_NAME = 'UX_StockTakeItemBag_Item_Bag') THEN
        CREATE UNIQUE INDEX `UX_StockTakeItemBag_Item_Bag`
            ON `StockTakeItemBag` (`StockTakeItemId`, `PaddyLotBagId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItemBag'
          AND INDEX_NAME = 'IX_StockTakeItemBag_PaddyLotBagId') THEN
        CREATE INDEX `IX_StockTakeItemBag_PaddyLotBagId` ON `StockTakeItemBag` (`PaddyLotBagId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItemBag'
          AND INDEX_NAME = 'IX_StockTakeItemBag_TargetLocationId') THEN
        CREATE INDEX `IX_StockTakeItemBag_TargetLocationId` ON `StockTakeItemBag` (`TargetLocationId`);
    END IF;
END;
CALL `_MigrateStockTakeItemBagIndexes`();
DROP PROCEDURE IF EXISTS `_MigrateStockTakeItemBagIndexes`;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `StockTakeItemBag`;");
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_RollbackStockTakeByBag`;
CREATE PROCEDURE `_RollbackStockTakeByBag`()
BEGIN
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake'
          AND CONSTRAINT_NAME = 'FK_StockTake_Location_ScopeLocationId') THEN
        ALTER TABLE `StockTake` DROP FOREIGN KEY `FK_StockTake_Location_ScopeLocationId`;
    END IF;
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake'
          AND CONSTRAINT_NAME = 'FK_StockTake_PaddyLot_ScopePaddyLotId') THEN
        ALTER TABLE `StockTake` DROP FOREIGN KEY `FK_StockTake_PaddyLot_ScopePaddyLotId`;
    END IF;
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTake' AND COLUMN_NAME = 'ScopeType') THEN
        ALTER TABLE `StockTake`
            DROP COLUMN `ScopeType`, DROP COLUMN `ScopeZoneName`,
            DROP COLUMN `ScopeLocationId`, DROP COLUMN `ScopePaddyLotId`,
            DROP COLUMN `IsQuarantineScope`;
    END IF;
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTakeItem' AND COLUMN_NAME = 'SystemBagCount') THEN
        ALTER TABLE `StockTakeItem`
            DROP COLUMN `SystemBagCount`, DROP COLUMN `CountedBagCount`,
            DROP COLUMN `VarianceReason`, DROP COLUMN `AdjustedBagCount`,
            DROP COLUMN `AdjustedWeightKg`;
    END IF;
END;
CALL `_RollbackStockTakeByBag`();
DROP PROCEDURE IF EXISTS `_RollbackStockTakeByBag`;
");
        }
    }
}

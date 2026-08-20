using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <summary>
    /// Chuyển kho nội bộ THEO BAO: bảng StockTransferBag ghi từng bao được chọn kèm
    /// kết quả kiểm định chất lượng ở kho nguồn và cách xử lý (TRANSFER/QUARANTINE/DISPOSE),
    /// lô nguồn, lô đích, ô cách ly. Thêm cột InboundOrder.StockTransferId để phiếu nhập
    /// tự sinh ở kho đích truy vết ngược về phiếu chuyển + kho nguồn.
    ///
    /// Migration viết tay nên PHẢI tự khai [DbContext] + [Migration]: EF nhận diện
    /// migration qua ATTRIBUTE (bình thường ở file .Designer.cs do CLI sinh). Thiếu attribute
    /// thì Database.Migrate() lúc khởi động bỏ qua file này, DB giữ schema cũ và API trả 500
    /// "Unknown column". Dùng INFORMATION_SCHEMA để chạy lại được nhiều lần trên MySQL 5.7+.
    /// </summary>
    [DbContext(typeof(BackendContext))]
    [Migration("20260820120000_AddStockTransferBagAndInboundTransferLink")]
    public partial class AddStockTransferBagAndInboundTransferLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Cột truy vết trên InboundOrder
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateInboundTransferLink`;
CREATE PROCEDURE `_MigrateInboundTransferLink`()
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder' AND COLUMN_NAME = 'StockTransferId') THEN
        ALTER TABLE `InboundOrder` ADD COLUMN `StockTransferId` int NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder'
          AND INDEX_NAME = 'IX_InboundOrder_StockTransferId') THEN
        CREATE INDEX `IX_InboundOrder_StockTransferId` ON `InboundOrder` (`StockTransferId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder'
          AND CONSTRAINT_NAME = 'FK_InboundOrder_StockTransfer_StockTransferId') THEN
        ALTER TABLE `InboundOrder` ADD CONSTRAINT `FK_InboundOrder_StockTransfer_StockTransferId`
            FOREIGN KEY (`StockTransferId`) REFERENCES `StockTransfer` (`Id`) ON DELETE RESTRICT;
    END IF;
END;
CALL `_MigrateInboundTransferLink`();
DROP PROCEDURE IF EXISTS `_MigrateInboundTransferLink`;
");

            // 2) Bảng StockTransferBag
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `StockTransferBag` (
    `Id`                  int NOT NULL AUTO_INCREMENT,
    `StockTransferItemId` int NOT NULL,
    `BagId`               int NOT NULL,
    `SourceLotId`         int NULL,
    `WeightKg`            decimal(18,3) NOT NULL,
    `MoisturePercent`     decimal(5,2) NULL,
    `ImpurityPercent`     decimal(5,2) NULL,
    `MoldLevel`           varchar(50) CHARACTER SET utf8mb4 NULL,
    `PestLevel`           varchar(50) CHARACTER SET utf8mb4 NULL,
    `PackagingStatus`     varchar(50) CHARACTER SET utf8mb4 NULL,
    `QualityResult`       varchar(30) CHARACTER SET utf8mb4 NULL,
    `QualityNote`         varchar(1000) CHARACTER SET utf8mb4 NULL,
    `Disposition`         varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `QuarantineLocationId` int NULL,
    `TargetLotId`         int NULL,
    `Note`                varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsDeleted`           tinyint(1) NOT NULL DEFAULT 0,
    `CreatedDate`         datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `LastModifiedDate`    datetime(6) NULL,
    `CreatedBy`           int NULL,
    `UpdatedBy`           int NULL,
    CONSTRAINT `PK_StockTransferBag` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StockTransferBag_StockTransferItem_StockTransferItemId`
        FOREIGN KEY (`StockTransferItemId`) REFERENCES `StockTransferItem` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_StockTransferBag_PaddyLotBag_BagId`
        FOREIGN KEY (`BagId`) REFERENCES `PaddyLotBag` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_StockTransferBag_PaddyLot_SourceLotId`
        FOREIGN KEY (`SourceLotId`) REFERENCES `PaddyLot` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_StockTransferBag_PaddyLot_TargetLotId`
        FOREIGN KEY (`TargetLotId`) REFERENCES `PaddyLot` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_StockTransferBag_Location_QuarantineLocationId`
        FOREIGN KEY (`QuarantineLocationId`) REFERENCES `Location` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;
");

            // 3) Chỉ mục cho StockTransferBag
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateStockTransferBagIndexes`;
CREATE PROCEDURE `_MigrateStockTransferBagIndexes`()
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTransferBag'
          AND INDEX_NAME = 'IX_StockTransferBag_StockTransferItemId') THEN
        CREATE INDEX `IX_StockTransferBag_StockTransferItemId` ON `StockTransferBag` (`StockTransferItemId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTransferBag'
          AND INDEX_NAME = 'IX_StockTransferBag_BagId') THEN
        CREATE INDEX `IX_StockTransferBag_BagId` ON `StockTransferBag` (`BagId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTransferBag'
          AND INDEX_NAME = 'IX_StockTransferBag_SourceLotId') THEN
        CREATE INDEX `IX_StockTransferBag_SourceLotId` ON `StockTransferBag` (`SourceLotId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTransferBag'
          AND INDEX_NAME = 'IX_StockTransferBag_TargetLotId') THEN
        CREATE INDEX `IX_StockTransferBag_TargetLotId` ON `StockTransferBag` (`TargetLotId`);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockTransferBag'
          AND INDEX_NAME = 'IX_StockTransferBag_QuarantineLocationId') THEN
        CREATE INDEX `IX_StockTransferBag_QuarantineLocationId` ON `StockTransferBag` (`QuarantineLocationId`);
    END IF;
END;
CALL `_MigrateStockTransferBagIndexes`();
DROP PROCEDURE IF EXISTS `_MigrateStockTransferBagIndexes`;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `StockTransferBag`;");
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_RollbackInboundTransferLink`;
CREATE PROCEDURE `_RollbackInboundTransferLink`()
BEGIN
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder'
          AND CONSTRAINT_NAME = 'FK_InboundOrder_StockTransfer_StockTransferId') THEN
        ALTER TABLE `InboundOrder` DROP FOREIGN KEY `FK_InboundOrder_StockTransfer_StockTransferId`;
    END IF;
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder'
          AND INDEX_NAME = 'IX_InboundOrder_StockTransferId') THEN
        DROP INDEX `IX_InboundOrder_StockTransferId` ON `InboundOrder`;
    END IF;
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InboundOrder' AND COLUMN_NAME = 'StockTransferId') THEN
        ALTER TABLE `InboundOrder` DROP COLUMN `StockTransferId`;
    END IF;
END;
CALL `_RollbackInboundTransferLink`();
DROP PROCEDURE IF EXISTS `_RollbackInboundTransferLink`;
");
        }
    }
}

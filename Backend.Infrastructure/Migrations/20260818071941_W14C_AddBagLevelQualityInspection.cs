using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class W14C_AddBagLevelQualityInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dùng stored procedure + INFORMATION_SCHEMA để tương thích MySQL 5.7+
            // (ADD COLUMN IF NOT EXISTS chỉ có từ MySQL 8.0.1)
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateW14C_AddQIColumns`;
CREATE PROCEDURE `_MigrateW14C_AddQIColumns`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspection'
          AND COLUMN_NAME  = 'CompletedAt'
    ) THEN
        ALTER TABLE `QualityInspection` ADD COLUMN `CompletedAt` datetime(6) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspection'
          AND COLUMN_NAME  = 'CompletedBy'
    ) THEN
        ALTER TABLE `QualityInspection` ADD COLUMN `CompletedBy` int NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspection'
          AND COLUMN_NAME  = 'InspectionType'
    ) THEN
        ALTER TABLE `QualityInspection` ADD COLUMN `InspectionType` varchar(30) CHARACTER SET utf8mb4 NULL;
    END IF;
END;
CALL `_MigrateW14C_AddQIColumns`();
DROP PROCEDURE IF EXISTS `_MigrateW14C_AddQIColumns`;
");

            // CREATE TABLE IF NOT EXISTS: bỏ qua nếu bảng đã tồn tại
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `QualityInspectionBagResult` (
    `Id`                   int NOT NULL AUTO_INCREMENT,
    `QualityInspectionId`  int NOT NULL,
    `BagId`                int NOT NULL,
    `InspectedAt`          datetime(6) NOT NULL,
    `InspectorId`          int NULL,
    `MoisturePercent`      decimal(5,2) NULL,
    `ImpurityPercent`      decimal(5,2) NULL,
    `MoldLevel`            varchar(50)  CHARACTER SET utf8mb4 NULL,
    `PestLevel`            varchar(50)  CHARACTER SET utf8mb4 NULL,
    `PackagingStatus`      varchar(50)  CHARACTER SET utf8mb4 NULL,
    `QualityResult`        varchar(30)  CHARACTER SET utf8mb4 NULL,
    `Disposition`          varchar(30)  CHARACTER SET utf8mb4 NULL,
    `Handling`             varchar(200) CHARACTER SET utf8mb4 NULL,
    `Note`                 varchar(1000) CHARACTER SET utf8mb4 NULL,
    `IsDeleted`            tinyint(1) NOT NULL DEFAULT 0,
    `CreatedDate`          datetime(6) NOT NULL,
    `LastModifiedDate`     datetime(6) NULL,
    `CreatedBy`            int NULL,
    `UpdatedBy`            int NULL,
    CONSTRAINT `PK_QualityInspectionBagResult` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_QualityInspectionBagResult_PaddyLotBag_BagId`
        FOREIGN KEY (`BagId`) REFERENCES `PaddyLotBag` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_QualityInspectionBagResult_QualityInspection_QualityInspecti~`
        FOREIGN KEY (`QualityInspectionId`) REFERENCES `QualityInspection` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_QualityInspectionBagResult_User_InspectorId`
        FOREIGN KEY (`InspectorId`) REFERENCES `User` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;");

            // INSERT IGNORE: bỏ qua nếu Id=7 đã tồn tại trong DB
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `LotStatus` (`Id`, `Code`, `Color`, `CreatedBy`, `CreatedDate`, `IsDeleted`, `LastModifiedDate`, `Name`, `UpdatedBy`) " +
                "VALUES (7, 'REJECTED_RETURN', '#DC2626', NULL, '2026-01-01 00:00:00', 0, NULL, 'Trả lại sau kiểm định', NULL);");

            // Dùng stored procedure + INFORMATION_SCHEMA.STATISTICS để tương thích MySQL 5.7+
            // (CREATE INDEX IF NOT EXISTS chỉ có từ MySQL 8.0.1)
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `_MigrateW14C_AddIndexes`;
CREATE PROCEDURE `_MigrateW14C_AddIndexes`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspectionBagResult'
          AND INDEX_NAME   = 'IX_QualityInspectionBagResult_BagId_InspectedAt'
    ) THEN
        CREATE INDEX `IX_QualityInspectionBagResult_BagId_InspectedAt`
            ON `QualityInspectionBagResult` (`BagId`, `InspectedAt`);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspectionBagResult'
          AND INDEX_NAME   = 'IX_QualityInspectionBagResult_InspectionId_Disposition'
    ) THEN
        CREATE INDEX `IX_QualityInspectionBagResult_InspectionId_Disposition`
            ON `QualityInspectionBagResult` (`QualityInspectionId`, `Disposition`);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspectionBagResult'
          AND INDEX_NAME   = 'IX_QualityInspectionBagResult_InspectorId'
    ) THEN
        CREATE INDEX `IX_QualityInspectionBagResult_InspectorId`
            ON `QualityInspectionBagResult` (`InspectorId`);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'QualityInspectionBagResult'
          AND INDEX_NAME   = 'UQ_QualityInspectionBagResult_InspectionId_BagId'
    ) THEN
        CREATE UNIQUE INDEX `UQ_QualityInspectionBagResult_InspectionId_BagId`
            ON `QualityInspectionBagResult` (`QualityInspectionId`, `BagId`);
    END IF;
END;
CALL `_MigrateW14C_AddIndexes`();
DROP PROCEDURE IF EXISTS `_MigrateW14C_AddIndexes`;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualityInspectionBagResult");

            // Chỉ xóa nếu Code đúng REJECTED_RETURN, tránh xóa nhầm dữ liệu thật đã có sẵn.
            migrationBuilder.Sql(
                "DELETE FROM `LotStatus` WHERE `Id` = 7 AND `Code` = 'REJECTED_RETURN';");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "QualityInspection");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "QualityInspection");

            migrationBuilder.DropColumn(
                name: "InspectionType",
                table: "QualityInspection");
        }
    }
}

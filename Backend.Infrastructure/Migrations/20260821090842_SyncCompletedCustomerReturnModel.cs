using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncCompletedCustomerReturnModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard: các cột này có thể đã được thêm bởi CompleteCustomerReturnWorkflow
            // trước khi migration này được scaffold. Dùng stored procedure để check
            // INFORMATION_SCHEMA — tương thích MySQL 5.7+ (ADD COLUMN IF NOT EXISTS chỉ có từ 8.0.4).
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddDisposition`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddDisposition`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME  = 'CustomerReturnOrderItemAllocation'
          AND COLUMN_NAME = 'Disposition'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            ADD COLUMN `Disposition` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'PENDING_INSPECTION';
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddDisposition`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddDisposition`;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddQuantityReceived`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddQuantityReceived`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME  = 'CustomerReturnOrderItemAllocation'
          AND COLUMN_NAME = 'QuantityReceived'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            ADD COLUMN `QuantityReceived` decimal(18,3) NOT NULL DEFAULT 0;
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddQuantityReceived`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddQuantityReceived`;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocationId`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddRejectedLocationId`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME  = 'CustomerReturnOrderItemAllocation'
          AND COLUMN_NAME = 'RejectedLocationId'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            ADD COLUMN `RejectedLocationId` int NULL;
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddRejectedLocationId`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocationId`;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectionReason`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddRejectionReason`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME  = 'CustomerReturnOrderItemAllocation'
          AND COLUMN_NAME = 'RejectionReason'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            ADD COLUMN `RejectionReason` varchar(500) CHARACTER SET utf8mb4 NULL;
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddRejectionReason`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectionReason`;");

            // Guard: tất cả cột CustomerReturnOrder cũng đã được thêm bởi CompleteCustomerReturnWorkflow.
            // Dùng stored procedure check INFORMATION_SCHEMA (tương thích MySQL 5.7+).
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddCustomerReturnOrderCols`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddCustomerReturnOrderCols`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'CancellationReason'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `CancellationReason` varchar(500) CHARACTER SET utf8mb4 NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'CancelledAt'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `CancelledAt` datetime(6) NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'CancelledByUserId'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `CancelledByUserId` int NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'InspectedAt'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `InspectedAt` datetime(6) NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'InspectedByUserId'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `InspectedByUserId` int NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'ReceivedAt'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `ReceivedAt` datetime(6) NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'ReceivedByUserId'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `ReceivedByUserId` int NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'RefundStatus'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `RefundStatus` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'NOT_APPLICABLE';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'RefundedAmount'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `RefundedAmount` decimal(18,2) NOT NULL DEFAULT 0;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'RejectedAt'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `RejectedAt` datetime(6) NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'RejectedByUserId'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `RejectedByUserId` int NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'RejectionReason'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `RejectionReason` varchar(500) CHARACTER SET utf8mb4 NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'SubmittedAt'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `SubmittedAt` datetime(6) NULL;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerReturnOrder' AND COLUMN_NAME = 'SubmittedByUserId'
    ) THEN
        ALTER TABLE `CustomerReturnOrder` ADD COLUMN `SubmittedByUserId` int NULL;
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddCustomerReturnOrderCols`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddCustomerReturnOrderCols`;");

            // Guard: dùng INSERT IGNORE để bỏ qua nếu rows Id=6,7,8 đã tồn tại
            migrationBuilder.Sql(@"
INSERT IGNORE INTO `CustomerReturnOrderStatus` (`Id`, `Code`, `Color`, `CreatedBy`, `CreatedDate`, `IsDeleted`, `LastModifiedDate`, `Name`, `UpdatedBy`)
VALUES
    (6, 'PENDING_APPROVAL', '#8B5CF6', NULL, '2026-01-01 00:00:00', FALSE, NULL, 'Chờ duyệt', NULL),
    (7, 'RECEIVED',         '#06B6D4', NULL, '2026-01-01 00:00:00', FALSE, NULL, 'Đã nhận hàng', NULL),
    (8, 'REJECTED',         '#DC2626', NULL, '2026-01-01 00:00:00', FALSE, NULL, 'Từ chối', NULL);
");
            // UPDATE luôn an toàn dù chạy nhiều lần
            migrationBuilder.Sql("UPDATE `CustomerReturnOrderStatus` SET `Name` = 'Hoàn tất' WHERE `Id` = 4;");

            // Guard index: chỉ tạo nếu chưa tồn tại (stored proc, tương thích MySQL 5.7+)
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocIdx`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddRejectedLocIdx`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME  = 'CustomerReturnOrderItemAllocation'
          AND INDEX_NAME  = 'IX_CustomerReturnOrderItemAllocation_RejectedLocationId'
    ) THEN
        CREATE INDEX `IX_CustomerReturnOrderItemAllocation_RejectedLocationId`
            ON `CustomerReturnOrderItemAllocation` (`RejectedLocationId`);
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddRejectedLocIdx`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocIdx`;");

            // Guard check constraints (drop-then-recreate — idempotent)
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddCkConstraints`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddCkConstraints`()
BEGIN
    -- Drop nếu đã tồn tại để tránh lỗi duplicate
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME       = 'CustomerReturnOrderItemAllocation'
          AND CONSTRAINT_NAME  = 'CK_CustomerReturnAllocation_CreditAmount'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            DROP CONSTRAINT `CK_CustomerReturnAllocation_CreditAmount`;
    END IF;
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME       = 'CustomerReturnOrderItemAllocation'
          AND CONSTRAINT_NAME  = 'CK_CustomerReturnAllocation_Quantities'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            DROP CONSTRAINT `CK_CustomerReturnAllocation_Quantities`;
    END IF;
    ALTER TABLE `CustomerReturnOrderItemAllocation`
        ADD CONSTRAINT `CK_CustomerReturnAllocation_CreditAmount`
            CHECK (CreditAmount >= 0 AND UnitCreditPrice >= 0),
        ADD CONSTRAINT `CK_CustomerReturnAllocation_Quantities`
            CHECK (QuantityReturned > 0 AND QuantityReceived >= 0 AND QuantityGood >= 0
                   AND QuantityDamaged >= 0 AND QuantityRejected >= 0 AND CreditQuantity >= 0);
END;");
            migrationBuilder.Sql("CALL `__Mig_AddCkConstraints`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddCkConstraints`;");

            // Guard FK: chỉ thêm nếu chưa tồn tại
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocFk`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_AddRejectedLocFk`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME      = 'CustomerReturnOrderItemAllocation'
          AND CONSTRAINT_NAME = 'FK_CustomerReturnOrderItemAllocation_Location_RejectedLocationId'
          AND CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) THEN
        ALTER TABLE `CustomerReturnOrderItemAllocation`
            ADD CONSTRAINT `FK_CustomerReturnOrderItemAllocation_Location_RejectedLocationId`
            FOREIGN KEY (`RejectedLocationId`) REFERENCES `Location` (`Id`) ON DELETE RESTRICT;
    END IF;
END;");
            migrationBuilder.Sql("CALL `__Mig_AddRejectedLocFk`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_AddRejectedLocFk`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReturnOrderItemAllocation_Location_RejectedLocationId",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReturnOrderItemAllocation_RejectedLocationId",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerReturnAllocation_CreditAmount",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerReturnAllocation_Quantities",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropColumn(
                name: "QuantityReceived",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropColumn(
                name: "RejectedLocationId",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "CustomerReturnOrderItemAllocation");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "InspectedAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "InspectedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "ReceivedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "CustomerReturnOrder");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "CustomerReturnOrder");

            migrationBuilder.UpdateData(
                table: "CustomerReturnOrderStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Đã nhận lại hàng");
        }
    }
}

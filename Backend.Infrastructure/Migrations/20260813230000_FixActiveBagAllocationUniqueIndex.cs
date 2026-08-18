using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Cho phép một bao có nhiều allocation lịch sử nhưng chỉ tối đa một allocation ACTIVE.
/// MySQL bỏ qua filter của filtered index cũ nên index BagId đã vô tình unique vĩnh viễn.
///
/// Viết idempotent (kiểm tra INFORMATION_SCHEMA trước mỗi bước) vì MySQL không rollback DDL:
/// lỗi giữa chừng thì dòng __EFMigrationsHistory không được ghi, lần chạy sau lặp lại từ đầu
/// và chết vì "Duplicate key name" / "Duplicate column name" -> container restart loop.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260813230000_FixActiveBagAllocationUniqueIndex")]
public partial class FixActiveBagAllocationUniqueIndex : Migration
{
    private const string ProcName = "sp__mig_20260813230000";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `__MigrationSkipLog` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Step` varchar(200) NOT NULL,
    `ErrorMessage` text NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
) DEFAULT CHARSET=utf8mb4;");

        migrationBuilder.Sql($@"
CREATE PROCEDURE `{ProcName}`()
BEGIN
    DECLARE v_msg TEXT DEFAULT '';

    -- BagId đang là cột FK; MySQL không cho bỏ index duy nhất đang hỗ trợ FK.
    -- Tạo index tạm trước để khóa ngoại luôn được bảo vệ trong lúc đổi index.
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId_FK_Temp') = 0 THEN
        CREATE INDEX `IX_PaddyLotBagAllocation_BagId_FK_Temp`
            ON `PaddyLotBagAllocation` (`BagId`);
    END IF;

    -- Chỉ bỏ index cũ nếu nó đang UNIQUE. Nếu đã là index thường thì migration đã chạy rồi.
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId'
           AND NON_UNIQUE = 0) > 0 THEN
        DROP INDEX `IX_PaddyLotBagAllocation_BagId` ON `PaddyLotBagAllocation`;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND COLUMN_NAME = 'ActiveBagId') = 0 THEN
        ALTER TABLE `PaddyLotBagAllocation`
            ADD COLUMN `ActiveBagId` int
            GENERATED ALWAYS AS (
                CASE WHEN `Status` = 'ACTIVE' AND `IsDeleted` = 0 THEN `BagId` ELSE NULL END
            ) STORED;
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId') = 0 THEN
        CREATE INDEX `IX_PaddyLotBagAllocation_BagId`
            ON `PaddyLotBagAllocation` (`BagId`);
    END IF;

    -- Unique index phụ thuộc DỮ LIỆU: lỗi nếu có bao nào đang >1 allocation ACTIVE.
    -- Ghi log rồi đi tiếp thay vì làm chết app. Kiểm tra sau bằng:
    --   SELECT BagId, COUNT(*) FROM PaddyLotBagAllocation
    --    WHERE Status = 'ACTIVE' AND IsDeleted = 0 GROUP BY BagId HAVING COUNT(*) > 1;
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_ActiveBagId') = 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
            BEGIN
                GET DIAGNOSTICS CONDITION 1 v_msg = MESSAGE_TEXT;
                INSERT INTO `__MigrationSkipLog` (`Step`, `ErrorMessage`, `CreatedAt`)
                VALUES ('IX_PaddyLotBagAllocation_ActiveBagId', v_msg, NOW(6));
            END;
            CREATE UNIQUE INDEX `IX_PaddyLotBagAllocation_ActiveBagId`
                ON `PaddyLotBagAllocation` (`ActiveBagId`);
        END;
    END IF;

    -- Bỏ index tạm; nếu khóa ngoại đang phải dựa vào nó thì giữ lại, không sao.
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId_FK_Temp') > 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION BEGIN END;
            DROP INDEX `IX_PaddyLotBagAllocation_BagId_FK_Temp` ON `PaddyLotBagAllocation`;
        END;
    END IF;
END;");

        migrationBuilder.Sql($"CALL `{ProcName}`();");
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");
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

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
/// nếu lỗi giữa chừng thì dòng __EFMigrationsHistory không được ghi, lần chạy sau sẽ lặp lại
/// từ đầu và chết vì "Duplicate key name" / "Duplicate column name".
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260813230000_FixActiveBagAllocationUniqueIndex")]
public partial class FixActiveBagAllocationUniqueIndex : Migration
{
    private const string ProcName = "sp__mig_20260813230000";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");

        migrationBuilder.Sql($@"
CREATE PROCEDURE `{ProcName}`()
BEGIN
    -- BagId đang là cột FK; MySQL không cho bỏ index duy nhất đang hỗ trợ FK.
    -- Tạo index tạm trước để khóa ngoại luôn được bảo vệ trong lúc đổi index.
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId_FK_Temp') = 0 THEN
        CREATE INDEX `IX_PaddyLotBagAllocation_BagId_FK_Temp`
            ON `PaddyLotBagAllocation` (`BagId`);
    END IF;

    -- Chỉ bỏ index cũ nếu nó đang UNIQUE (NON_UNIQUE = 0). Nếu đã là index thường
    -- thì migration đã chạy trước đó rồi -> giữ nguyên.
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

    -- LƯU Ý DỮ LIỆU: câu này sẽ lỗi 'Duplicate entry' nếu DB đang có bao nào
    -- có >1 allocation ACTIVE. Kiểm tra trước bằng:
    --   SELECT BagId, COUNT(*) FROM PaddyLotBagAllocation
    --    WHERE Status = 'ACTIVE' AND IsDeleted = 0 GROUP BY BagId HAVING COUNT(*) > 1;
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_ActiveBagId') = 0 THEN
        CREATE UNIQUE INDEX `IX_PaddyLotBagAllocation_ActiveBagId`
            ON `PaddyLotBagAllocation` (`ActiveBagId`);
    END IF;

    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PaddyLotBagAllocation'
           AND INDEX_NAME = 'IX_PaddyLotBagAllocation_BagId_FK_Temp') > 0 THEN
        DROP INDEX `IX_PaddyLotBagAllocation_BagId_FK_Temp` ON `PaddyLotBagAllocation`;
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

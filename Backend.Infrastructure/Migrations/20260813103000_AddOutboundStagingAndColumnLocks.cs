using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Thêm khu chờ xuất (staging) + khóa cột cho phiếu xuất, và truy vết bao nguồn.
///
/// HAI VẤN ĐỀ ĐÃ SỬA SO VỚI BẢN ĐẦU (đều làm chết API lúc khởi động trên server):
///
/// 1) "Duplicate column name 'IsOutboundStaging'"
///    MySQL KHÔNG rollback DDL — mỗi ALTER TABLE tự commit. Migration lỗi giữa chừng thì các cột
///    đã tạo vẫn nằm lại nhưng dòng ghi nhận trong `__EFMigrationsHistory` KHÔNG được ghi.
///    Lần khởi động sau EF chạy lại từ đầu -> trùng cột -> container restart loop.
///    => Mọi thao tác đều kiểm tra INFORMATION_SCHEMA trước, chạy lại bao nhiêu lần cũng an toàn.
///
/// 2) "Cannot add foreign key constraint" (MySQL 1215)
///    FK_PaddyLotBag_PaddyLotBag_SourceBagId là khóa ngoại TỰ THAM CHIẾU với ON DELETE SET NULL,
///    trong khi chính bảng PaddyLotBag lại là con của FK_PaddyLotBag_PaddyLot_LotId ON DELETE CASCADE.
///    InnoDB từ chối cấu hình này: xóa 1 PaddyLot sẽ vừa CASCADE xóa các bao, vừa phải SET NULL
///    các bao khác trong cùng bảng -> nhập nhằng, bị chặn ngay từ lúc tạo constraint.
///    Đối chiếu DB local đang chạy tốt: khóa ngoại tự tham chiếu duy nhất ở đó là
///    FK_ProductCategory_ProductCategory_ParentCategoryId và nó dùng ON DELETE RESTRICT.
///    => Đổi sang RESTRICT. Bao lúa dùng xóa mềm (IsDeleted) nên thực tế không có xóa cứng,
///       RESTRICT không đổi hành vi nghiệp vụ. PaddyLotBagConfiguration đã sửa cho khớp.
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

        // Bảng ghi lại các bước bị bỏ qua + thông điệp lỗi gốc của MySQL.
        // MigrationExtensions đọc bảng này rồi in ra Serilog, nhờ đó xem được nguyên nhân
        // qua tab Logs mà không cần kết nối trực tiếp vào database trên server.
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

    -- ---------- Index ----------
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

    -- ---------- Khóa ngoại (KHÔNG được phép làm chết app) ----------
    -- Khóa ngoại chỉ là ràng buộc toàn vẹn ở tầng DB; quan hệ đã do EF quản lý ở tầng code
    -- nên thiếu nó không làm sai truy vấn hay thiếu cột. Ngược lại nếu để nó ném lỗi thì
    -- API chết ngay lúc khởi động và không vào được DB để sửa.
    -- Mỗi FK chạy trong block có CONTINUE HANDLER: lỗi thì ghi lý do vào __MigrationSkipLog
    -- và đi tiếp. Đọc lý do đó ở tab Logs (MigrationExtensions in ra sau khi migrate xong).
    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_Location_OutboundOrder_OutboundLockOrderId') = 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
            BEGIN
                GET DIAGNOSTICS CONDITION 1 v_msg = MESSAGE_TEXT;
                INSERT INTO `__MigrationSkipLog` (`Step`, `ErrorMessage`, `CreatedAt`)
                VALUES ('FK_Location_OutboundOrder_OutboundLockOrderId', v_msg, NOW(6));
            END;
            ALTER TABLE `Location`
                ADD CONSTRAINT `FK_Location_OutboundOrder_OutboundLockOrderId`
                FOREIGN KEY (`OutboundLockOrderId`) REFERENCES `OutboundOrder` (`Id`) ON DELETE SET NULL;
        END;
    END IF;

    -- RESTRICT chứ KHÔNG phải SET NULL — xem giải thích ở phần tóm tắt đầu file.
    IF (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
         WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY'
           AND CONSTRAINT_NAME = 'FK_PaddyLotBag_PaddyLotBag_SourceBagId') = 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
            BEGIN
                GET DIAGNOSTICS CONDITION 1 v_msg = MESSAGE_TEXT;
                INSERT INTO `__MigrationSkipLog` (`Step`, `ErrorMessage`, `CreatedAt`)
                VALUES ('FK_PaddyLotBag_PaddyLotBag_SourceBagId', v_msg, NOW(6));
            END;
            ALTER TABLE `PaddyLotBag`
                ADD CONSTRAINT `FK_PaddyLotBag_PaddyLotBag_SourceBagId`
                FOREIGN KEY (`SourceBagId`) REFERENCES `PaddyLotBag` (`Id`) ON DELETE RESTRICT;
        END;
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
    -- BẮT BUỘC phải có: LocationConfiguration khai báo shadow property OutboundStagingWarehouseId
    -- nên EF đưa cột này vào MỌI câu SELECT từ bảng Location. Thiếu cột = hỏng toàn bộ màn kho.
    --
    -- VIRTUAL chứ KHÔNG phải STORED. Đây chính là câu lệnh đã làm chết API:
    -- thêm cột sinh STORED buộc MySQL COPY & DỰNG LẠI toàn bộ bảng Location, và trong bước
    -- dựng lại đó InnoDB phải tạo lại mọi khóa ngoại liên quan tới Location -> thất bại với
    -- thông điệp "Cannot add foreign key constraint" (Location có 4 FK đi ra + 1 FK đi vào
    -- từ PaddyLotBag.LocationId, trên bảng đang có 58 dòng dữ liệu thật).
    -- Cột VIRTUAL chỉ là thay đổi metadata (ALGORITHM=INPLACE), không dựng lại bảng,
    -- không đụng tới khóa ngoại. MySQL 5.7+ vẫn cho tạo unique index trên cột virtual,
    -- nên ràng buộc "mỗi kho 1 khu chờ xuất" giữ nguyên hiệu lực.
    IF (SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND COLUMN_NAME = 'OutboundStagingWarehouseId') = 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
            BEGIN
                GET DIAGNOSTICS CONDITION 1 v_msg = MESSAGE_TEXT;
                INSERT INTO `__MigrationSkipLog` (`Step`, `ErrorMessage`, `CreatedAt`)
                VALUES ('Location.OutboundStagingWarehouseId', v_msg, NOW(6));
            END;
            ALTER TABLE `Location`
                ADD COLUMN `OutboundStagingWarehouseId` int
                GENERATED ALWAYS AS (
                    CASE WHEN `IsOutboundStaging` = 1 AND `IsDeleted` = 0 THEN `WarehouseId` ELSE NULL END
                ) VIRTUAL;
        END;
    END IF;

    -- Unique index cũng không được làm chết app: nếu dữ liệu hiện có đang vi phạm
    -- (một kho có >1 khu chờ xuất) thì ghi log và bỏ qua.
    IF (SELECT COUNT(*) FROM information_schema.STATISTICS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Location'
           AND INDEX_NAME = 'UX_Location_OneOutboundStagingPerWarehouse') = 0 THEN
        BEGIN
            DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
            BEGIN
                GET DIAGNOSTICS CONDITION 1 v_msg = MESSAGE_TEXT;
                INSERT INTO `__MigrationSkipLog` (`Step`, `ErrorMessage`, `CreatedAt`)
                VALUES ('UX_Location_OneOutboundStagingPerWarehouse', v_msg, NOW(6));
            END;
            CREATE UNIQUE INDEX `UX_Location_OneOutboundStagingPerWarehouse`
                ON `Location` (`OutboundStagingWarehouseId`);
        END;
    END IF;
END;");

        migrationBuilder.Sql($"CALL `{ProcName}`();");
        migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{ProcName}`;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.DependencyInjection.Extentions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BackendContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        var pending = dbContext.Database.GetPendingMigrations().ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("[MIGRATION] Database đã cập nhật, không có migration nào đang chờ.");
            ReportSkippedSteps(dbContext, logger);
            return;
        }

        logger.LogWarning("[MIGRATION] {Count} migration đang chờ: {Pending}",
            pending.Count, string.Join(" -> ", pending));

        // TUYỆT ĐỐI KHÔNG dùng IMigrator.Migrate("tên-migration") để chạy từng cái một.
        // Tham số đó là TRẠNG THÁI ĐÍCH, không phải "chạy thêm cái này": EF sẽ REVERT
        // (chạy Down) mọi migration đã áp dụng có Id LỚN HƠN đích.
        // DB này đang có 20260813230000 applied nhưng 20260813103000 thì chưa (Id nhỏ hơn),
        // nên gọi Migrate("...103000") đã khiến EF revert 20260813230000 và làm chết app.
        // Database.Migrate() không tham số chỉ áp dụng phần còn thiếu, không bao giờ revert.
        try
        {
            dbContext.Database.Migrate();
            logger.LogInformation("[MIGRATION] Đã áp dụng xong {Count} migration.", pending.Count);
        }
        catch (Exception ex)
        {
            var stuckAt = FindStuckMigration(dbContext, pending);
            logger.LogCritical(ex, "[MIGRATION] CHẾT tại migration {Name}", stuckAt);
            LogSchemaReport(dbContext, logger);
            throw;
        }

        ReportSkippedSteps(dbContext, logger);
    }

    /// <summary>Migration đầu tiên trong hàng chờ mà vẫn chưa được ghi nhận là đã áp dụng.</summary>
    private static string FindStuckMigration(DbContext dbContext, List<string> pending)
    {
        try
        {
            var applied = dbContext.Database.GetAppliedMigrations().ToHashSet();
            return pending.FirstOrDefault(p => !applied.Contains(p)) ?? "(không xác định)";
        }
        catch
        {
            return pending.FirstOrDefault() ?? "(không xác định)";
        }
    }

    /// <summary>
    /// Các bước bị bỏ qua có chủ ý trong migration (khóa ngoại / unique index không tạo được).
    /// Ghi ra log để đọc được qua tab Logs mà không cần kết nối trực tiếp vào DB.
    /// </summary>
    private static void ReportSkippedSteps(DbContext dbContext, ILogger logger)
    {
        const string sql = @"
            SELECT `Step`, `ErrorMessage`, `CreatedAt`
            FROM `__MigrationSkipLog`
            ORDER BY `Id` DESC LIMIT 50;";

        try
        {
            var rows = QueryRows(dbContext, sql);
            if (rows.Count == 0) return;

            logger.LogWarning(
                "[MIGRATION] ===== {Count} BƯỚC BỊ BỎ QUA (schema chưa đầy đủ 100%) =====\n{Rows}",
                rows.Count, string.Join("\n", rows));
        }
        catch
        {
            // Bảng chưa tồn tại -> chưa có bước nào bị bỏ qua, không phải lỗi.
        }
    }

    /// <summary>
    /// In cấu trúc các bảng hay dính lỗi migration: engine, collation, kiểu cột, khóa ngoại.
    /// Nhiều lỗi MySQL dùng chung một thông điệp mơ hồ nên phải nhìn cấu trúc mới biết nguyên nhân.
    /// </summary>
    private static void LogSchemaReport(DbContext dbContext, ILogger logger)
    {
        const string tablesSql = @"
            SELECT CONCAT(TABLE_NAME, ' | engine=', IFNULL(ENGINE,'?'),
                          ' | collation=', IFNULL(TABLE_COLLATION,'?')) AS Info
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('Location','OutboundOrder','PaddyLotBag','PaddyLotBagAllocation','Warehouse','PaddyLot')
            ORDER BY TABLE_NAME;";

        const string columnsSql = @"
            SELECT CONCAT(TABLE_NAME, '.', COLUMN_NAME, ' | ', COLUMN_TYPE,
                          ' | nullable=', IS_NULLABLE, ' | ', IFNULL(EXTRA,'')) AS Info
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND ( (TABLE_NAME = 'Location' AND COLUMN_NAME IN ('Id','WarehouseId','OutboundLockOrderId','OutboundLockedAt','IsOutboundStaging','OutboundStagingWarehouseId'))
                 OR (TABLE_NAME = 'OutboundOrder' AND COLUMN_NAME = 'Id')
                 OR (TABLE_NAME = 'PaddyLotBag' AND COLUMN_NAME IN ('Id','LotId','SourceBagId'))
                 OR (TABLE_NAME = 'PaddyLotBagAllocation' AND COLUMN_NAME IN ('Id','BagId','ActiveBagId')) )
            ORDER BY TABLE_NAME, COLUMN_NAME;";

        const string indexSql = @"
            SELECT CONCAT(TABLE_NAME, ' | ', INDEX_NAME, ' | unique=', IF(NON_UNIQUE = 0,'YES','NO'),
                          ' | cols=', GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)) AS Info
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('Location','PaddyLotBag','PaddyLotBagAllocation')
            GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE
            ORDER BY TABLE_NAME, INDEX_NAME;";

        const string fkSql = @"
            SELECT CONCAT(CONSTRAINT_NAME, ' : ', TABLE_NAME, '.', COLUMN_NAME,
                          ' -> ', REFERENCED_TABLE_NAME, '.', REFERENCED_COLUMN_NAME) AS Info
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL
              AND TABLE_NAME IN ('Location','PaddyLotBag','PaddyLotBagAllocation')
            ORDER BY CONSTRAINT_NAME;";

        const string historySql = @"
            SELECT `MigrationId` FROM `__EFMigrationsHistory`
            ORDER BY `MigrationId` DESC LIMIT 15;";

        TryLogQuery(dbContext, logger, "BẢNG", tablesSql);
        TryLogQuery(dbContext, logger, "CỘT", columnsSql);
        TryLogQuery(dbContext, logger, "INDEX", indexSql);
        TryLogQuery(dbContext, logger, "KHÓA NGOẠI HIỆN CÓ", fkSql);
        TryLogQuery(dbContext, logger, "MIGRATION ĐÃ GHI NHẬN (15 mới nhất)", historySql);
    }

    private static void TryLogQuery(DbContext dbContext, ILogger logger, string title, string sql)
    {
        try
        {
            var rows = QueryRows(dbContext, sql);
            logger.LogCritical("[MIGRATION][CHẨN ĐOÁN] {Title}:\n{Rows}", title, string.Join("\n", rows));
        }
        catch (Exception ex)
        {
            logger.LogError("[MIGRATION][CHẨN ĐOÁN] Không đọc được {Title}: {Message}", title, ex.Message);
        }
    }

    private static List<string> QueryRows(DbContext dbContext, string sql)
    {
        var result = new List<string>();
        var connection = dbContext.Database.GetDbConnection();
        var opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
            opened = true;
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sb = new StringBuilder("  - ");
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) sb.Append("  |  ");
                    sb.Append(reader.IsDBNull(i) ? "(null)" : reader.GetValue(i)?.ToString());
                }
                result.Add(sb.ToString());
            }
        }
        finally
        {
            if (opened) connection.Close();
        }

        return result;
    }
}

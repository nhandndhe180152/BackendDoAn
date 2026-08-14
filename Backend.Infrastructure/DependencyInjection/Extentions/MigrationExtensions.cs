using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

        // Chạy TỪNG migration một để log chỉ đúng tên migration lỗi.
        // (MySQL không rollback DDL nên biết chính xác chỗ chết là bắt buộc.)
        var migrator = dbContext.GetService<IMigrator>();
        foreach (var name in pending)
        {
            logger.LogWarning("[MIGRATION] >>> Đang chạy {Name}", name);
            try
            {
                migrator.Migrate(name);
                logger.LogWarning("[MIGRATION] <<< Xong {Name}", name);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "[MIGRATION] CHẾT tại migration {Name}", name);
                LogSchemaReport(dbContext, logger);
                LogMigrationSql(migrator, name, logger);
                throw;
            }
        }

        logger.LogInformation("[MIGRATION] Đã áp dụng xong {Count} migration.", pending.Count);
        ReportSkippedSteps(dbContext, logger);
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
                "[MIGRATION] ===== CÓ {Count} BƯỚC BỊ BỎ QUA (schema chưa đầy đủ 100%) =====\n{Rows}",
                rows.Count, string.Join("\n", rows));
        }
        catch
        {
            // Bảng chưa tồn tại (chưa migration nào ghi vào) -> bỏ qua, không phải lỗi.
        }
    }

    /// <summary>
    /// In cấu trúc các bảng liên quan tới khóa ngoại hay lỗi: engine, collation, kiểu cột.
    /// Khóa ngoại InnoDB -> bảng MyISAM, hoặc lệch kiểu cột, đều báo cùng một thông điệp
    /// mơ hồ "Cannot add foreign key constraint" nên phải nhìn cấu trúc mới biết.
    /// </summary>
    private static void LogSchemaReport(DbContext dbContext, ILogger logger)
    {
        const string tablesSql = @"
            SELECT CONCAT(TABLE_NAME, ' | engine=', IFNULL(ENGINE,'?'),
                          ' | collation=', IFNULL(TABLE_COLLATION,'?'),
                          ' | rows=', IFNULL(TABLE_ROWS,0)) AS Info
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('Location','OutboundOrder','PaddyLotBag','PaddyLotBagAllocation','Warehouse','PaddyLot')
            ORDER BY TABLE_NAME;";

        const string columnsSql = @"
            SELECT CONCAT(TABLE_NAME, '.', COLUMN_NAME, ' | ', COLUMN_TYPE,
                          ' | nullable=', IS_NULLABLE,
                          ' | ', IFNULL(EXTRA,'')) AS Info
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND ( (TABLE_NAME = 'Location'    AND COLUMN_NAME IN ('Id','WarehouseId','OutboundLockOrderId','IsOutboundStaging','OutboundStagingWarehouseId'))
                 OR (TABLE_NAME = 'OutboundOrder' AND COLUMN_NAME = 'Id')
                 OR (TABLE_NAME = 'PaddyLotBag' AND COLUMN_NAME IN ('Id','LotId','SourceBagId')) )
            ORDER BY TABLE_NAME, COLUMN_NAME;";

        const string fkSql = @"
            SELECT CONCAT(CONSTRAINT_NAME, ' : ', TABLE_NAME, '.', COLUMN_NAME,
                          ' -> ', REFERENCED_TABLE_NAME, '.', REFERENCED_COLUMN_NAME) AS Info
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
              AND REFERENCED_TABLE_NAME IS NOT NULL
              AND TABLE_NAME IN ('Location','PaddyLotBag')
            ORDER BY CONSTRAINT_NAME;";

        TryLogQuery(dbContext, logger, "BẢNG", tablesSql);
        TryLogQuery(dbContext, logger, "CỘT", columnsSql);
        TryLogQuery(dbContext, logger, "KHÓA NGOẠI HIỆN CÓ", fkSql);
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

    /// <summary>In SQL mà EF sinh ra cho đúng migration bị lỗi, để thấy câu lệnh nào chết.</summary>
    private static void LogMigrationSql(IMigrator migrator, string name, ILogger logger)
    {
        try
        {
            var script = migrator.GenerateScript(fromMigration: name, toMigration: name);
            logger.LogCritical("[MIGRATION][CHẨN ĐOÁN] SQL của {Name}:\n{Script}", name, script);
        }
        catch (Exception ex)
        {
            logger.LogError("[MIGRATION][CHẨN ĐOÁN] Không sinh được SQL cho {Name}: {Message}", name, ex.Message);
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.BackgroundJobs.LowStock;

public class LowStockDetectionService : ILowStockDetectionService
{
    private readonly IApplicationDbContext _context;
    private readonly ILowStockQueryService _queryService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<LowStockDetectionService> _logger;

    public LowStockDetectionService(
        IApplicationDbContext context,
        ILowStockQueryService queryService,
        INotificationDispatcher notificationDispatcher,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _queryService = queryService;
        _notificationDispatcher = notificationDispatcher;
        _logger = loggerFactory.CreateLogger<LowStockDetectionService>();
    }

    public async Task<LowStockJobResult> DetectLowStockAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LowStockJobResult();

        // 1. Chạy trong transaction phù hợp nếu không phải InMemory
        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // 2. Lấy toàn bộ snapshots tồn kho khả dụng của các SKU có ngưỡng
            var snapshots = await _queryService.GetLowStockSnapshotsAsync(cancellationToken);
            result.Processed = snapshots.Count;

            // 3. Load toàn bộ Alert LOW_STOCK đang hoạt động (OPEN hoặc ACKNOWLEDGED)
            var activeAlerts = await _context.Alerts
                .Where(a => a.AlertType == AlertConstants.Type.LowStock && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved)
                .ToListAsync(cancellationToken);

            var activeAlertsMap = activeAlerts
                .Where(a => a.ProductVariantId.HasValue)
                .ToDictionary(a => (a.WarehouseId, a.ProductVariantId!.Value), a => a);

            var alertsToInsert = new List<Alert>();
            var notificationsToSend = new List<(LowStockSnapshotDto Snapshot, string Severity)>();

            foreach (var snapshot in snapshots)
            {
                activeAlertsMap.TryGetValue((snapshot.WarehouseId, snapshot.ProductVariantId), out var activeAlert);

                if (snapshot.AvailableKg <= snapshot.ThresholdKg)
                {
                    // Tồn kho thấp -> Xác định Severity
                    string severity = AlertConstants.Severity.Info;
                    if (snapshot.AvailableKg <= 0m)
                    {
                        severity = AlertConstants.Severity.Critical;
                    }
                    else if (snapshot.AvailableKg <= snapshot.ThresholdKg * 0.5m)
                    {
                        severity = AlertConstants.Severity.Warning;
                    }

                    var message = $"SKU \"{snapshot.SKU} - {snapshot.ProductName}\" tại kho \"{snapshot.WarehouseCode}\" chỉ còn {snapshot.AvailableKg:0.##} kg khả dụng (dưới hoặc bằng mức tối thiểu {snapshot.ThresholdKg:0.##} kg).";

                    if (activeAlert == null)
                    {
                        // Chưa có Alert -> Tạo mới
                        var newAlert = new Alert
                        {
                            AlertType = AlertConstants.Type.LowStock,
                            Severity = severity,
                            WarehouseId = snapshot.WarehouseId,
                            ProductVariantId = snapshot.ProductVariantId,
                            RelatedEntityType = "PRODUCT_VARIANT",
                            RelatedEntityId = snapshot.ProductVariantId,
                            Status = AlertConstants.Status.Open,
                            Message = message,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = $"LOW_STOCK:{snapshot.WarehouseId}:{snapshot.ProductVariantId}"
                        };
                        alertsToInsert.Add(newAlert);
                        result.Created++;

                        // Đăng ký gửi notification
                        notificationsToSend.Add((snapshot, severity));
                    }
                    else
                    {
                        // Đã có Alert -> Cập nhật nếu số liệu/severity thay đổi
                        bool hasChange = false;
                        var oldSeverity = activeAlert.Severity;

                        if (activeAlert.Severity != severity)
                        {
                            activeAlert.Severity = severity;
                            hasChange = true;
                        }

                        if (activeAlert.Message != message)
                        {
                            activeAlert.Message = message;
                            hasChange = true;
                        }

                        if (hasChange)
                        {
                            activeAlert.LastModifiedDate = DateTime.UtcNow;
                            _context.Alerts.Update(activeAlert);
                            result.Updated++;

                            // Gửi notification nếu Severity tăng lên
                            if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                            {
                                notificationsToSend.Add((snapshot, severity));
                            }
                        }
                    }
                }
                else
                {
                    // Tồn kho đã phục hồi (> Ngưỡng) -> Resolve Alert đang active nếu có
                    if (activeAlert != null)
                    {
                        activeAlert.Status = AlertConstants.Status.Resolved;
                        activeAlert.ResolvedAt = DateTime.UtcNow;
                        activeAlert.LastModifiedDate = DateTime.UtcNow;
                        activeAlert.DeduplicationKey = null;
                        _context.Alerts.Update(activeAlert);
                        result.Resolved++;
                    }
                }
            }

            // Lưu các alert tạo mới
            if (alertsToInsert.Count > 0)
            {
                await _context.Alerts.AddRangeAsync(alertsToInsert, cancellationToken);
            }

            // 4. Lưu thay đổi xuống Database
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
            {
                _logger.LogWarning(dbEx, "[LowStockDetection] Phát hiện xung đột DeduplicationKey khi thêm mới Alert. Thực hiện cập nhật thay vì chèn mới.");
                
                if (_context is DbContext dbContext)
                {
                    foreach (var entry in dbContext.ChangeTracker.Entries<Alert>().Where(e => e.State == EntityState.Added).ToList())
                    {
                        entry.State = EntityState.Detached;
                    }
                }

                var latestActiveAlerts = await _context.Alerts
                    .Where(a => a.AlertType == AlertConstants.Type.LowStock && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved)
                    .ToListAsync(cancellationToken);

                var latestActiveMap = latestActiveAlerts
                    .Where(a => a.ProductVariantId.HasValue)
                    .ToDictionary(a => (a.WarehouseId, a.ProductVariantId!.Value), a => a);

                foreach (var alertToInsert in alertsToInsert)
                {
                    if (alertToInsert.ProductVariantId.HasValue &&
                        latestActiveMap.TryGetValue((alertToInsert.WarehouseId, alertToInsert.ProductVariantId.Value), out var existingAlert))
                    {
                        bool hasChange = false;
                        if (existingAlert.Severity != alertToInsert.Severity)
                        {
                            existingAlert.Severity = alertToInsert.Severity;
                            hasChange = true;
                        }
                        if (existingAlert.Message != alertToInsert.Message)
                        {
                            existingAlert.Message = alertToInsert.Message;
                            hasChange = true;
                        }
                        if (hasChange)
                        {
                            existingAlert.LastModifiedDate = DateTime.UtcNow;
                            _context.Alerts.Update(existingAlert);
                            result.Updated++;
                        }
                    }
                    else
                    {
                        var cleanAlert = new Alert
                        {
                            AlertType = alertToInsert.AlertType,
                            Severity = alertToInsert.Severity,
                            WarehouseId = alertToInsert.WarehouseId,
                            ProductVariantId = alertToInsert.ProductVariantId,
                            RelatedEntityType = alertToInsert.RelatedEntityType,
                            RelatedEntityId = alertToInsert.RelatedEntityId,
                            Status = alertToInsert.Status,
                            Message = alertToInsert.Message,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = alertToInsert.DeduplicationKey
                        };
                        await _context.Alerts.AddAsync(cleanAlert, cancellationToken);
                        result.Created++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // 5. Gửi Notifications ngoài transaction (đảm bảo không làm hỏng dữ liệu DB nếu notification thất bại)
            foreach (var (snap, sev) in notificationsToSend)
            {
                await SendLowStockNotificationAsync(snap, sev);
            }
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "[LowStockDetection] Lỗi khi thực thi phát hiện tồn kho thấp.");
            result.Failed++;
        }

        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    public async Task DetectLowStockForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken)
    {
        // 1. Chạy trong transaction phù hợp nếu không phải InMemory
        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // 2. Lấy snapshot tồn kho khả dụng của SKU cụ thể tại kho cụ thể
            var snapshot = await _queryService.GetLowStockSnapshotForProductAsync(warehouseId, productVariantId, cancellationToken);
            if (snapshot == null) return;

            // 3. Load Alert LOW_STOCK đang hoạt động cho SKU này
            var activeAlert = await _context.Alerts
                .FirstOrDefaultAsync(a =>
                    a.AlertType == AlertConstants.Type.LowStock &&
                    a.WarehouseId == warehouseId &&
                    a.ProductVariantId == productVariantId &&
                    !a.IsDeleted &&
                    a.Status != AlertConstants.Status.Resolved,
                    cancellationToken);

            bool shouldSendNotification = false;
            string severityToSend = AlertConstants.Severity.Info;

            if (snapshot.AvailableKg <= snapshot.ThresholdKg)
            {
                string severity = AlertConstants.Severity.Info;
                if (snapshot.AvailableKg <= 0m)
                {
                    severity = AlertConstants.Severity.Critical;
                }
                else if (snapshot.AvailableKg <= snapshot.ThresholdKg * 0.5m)
                {
                    severity = AlertConstants.Severity.Warning;
                }

                var message = $"SKU \"{snapshot.SKU} - {snapshot.ProductName}\" tại kho \"{snapshot.WarehouseCode}\" chỉ còn {snapshot.AvailableKg:0.##} kg khả dụng (dưới hoặc bằng mức tối thiểu {snapshot.ThresholdKg:0.##} kg).";

                if (activeAlert == null)
                {
                    var newAlert = new Alert
                    {
                        AlertType = AlertConstants.Type.LowStock,
                        Severity = severity,
                        WarehouseId = snapshot.WarehouseId,
                        ProductVariantId = snapshot.ProductVariantId,
                        RelatedEntityType = "PRODUCT_VARIANT",
                        RelatedEntityId = snapshot.ProductVariantId,
                        Status = AlertConstants.Status.Open,
                        Message = message,
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false,
                        DeduplicationKey = $"LOW_STOCK:{snapshot.WarehouseId}:{snapshot.ProductVariantId}"
                    };
                    await _context.Alerts.AddAsync(newAlert, cancellationToken);
                    shouldSendNotification = true;
                    severityToSend = severity;
                }
                else
                {
                    bool hasChange = false;
                    var oldSeverity = activeAlert.Severity;

                    if (activeAlert.Severity != severity)
                    {
                        activeAlert.Severity = severity;
                        hasChange = true;
                    }

                    if (activeAlert.Message != message)
                    {
                        activeAlert.Message = message;
                        hasChange = true;
                    }

                    if (hasChange)
                    {
                        activeAlert.LastModifiedDate = DateTime.UtcNow;
                        _context.Alerts.Update(activeAlert);

                        if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                        {
                            shouldSendNotification = true;
                            severityToSend = severity;
                        }
                    }
                }
            }
            else
            {
                if (activeAlert != null)
                {
                    activeAlert.Status = AlertConstants.Status.Resolved;
                    activeAlert.ResolvedAt = DateTime.UtcNow;
                    activeAlert.LastModifiedDate = DateTime.UtcNow;
                    activeAlert.DeduplicationKey = null;
                    _context.Alerts.Update(activeAlert);
                }
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
            {
                _logger.LogWarning(dbEx, "[LowStockDetection] Phát hiện xung đột DeduplicationKey khi thêm mới Alert cho SKU {ProductVariantId}. Thực hiện cập nhật thay vì chèn mới.", productVariantId);

                if (_context is DbContext dbContext)
                {
                    foreach (var entry in dbContext.ChangeTracker.Entries<Alert>().Where(e => e.State == EntityState.Added).ToList())
                    {
                        entry.State = EntityState.Detached;
                    }
                }

                var existingAlert = await _context.Alerts
                    .FirstOrDefaultAsync(a =>
                        a.AlertType == AlertConstants.Type.LowStock &&
                        a.WarehouseId == warehouseId &&
                        a.ProductVariantId == productVariantId &&
                        !a.IsDeleted &&
                        a.Status != AlertConstants.Status.Resolved,
                        cancellationToken);

                if (existingAlert != null)
                {
                    string severity = AlertConstants.Severity.Info;
                    if (snapshot.AvailableKg <= 0m)
                    {
                        severity = AlertConstants.Severity.Critical;
                    }
                    else if (snapshot.AvailableKg <= snapshot.ThresholdKg * 0.5m)
                    {
                        severity = AlertConstants.Severity.Warning;
                    }

                    var message = $"SKU \"{snapshot.SKU} - {snapshot.ProductName}\" tại kho \"{snapshot.WarehouseCode}\" chỉ còn {snapshot.AvailableKg:0.##} kg khả dụng (dưới hoặc bằng mức tối thiểu {snapshot.ThresholdKg:0.##} kg).";

                    bool hasChange = false;
                    var oldSeverity = existingAlert.Severity;

                    if (existingAlert.Severity != severity)
                    {
                        existingAlert.Severity = severity;
                        hasChange = true;
                    }
                    if (existingAlert.Message != message)
                    {
                        existingAlert.Message = message;
                        hasChange = true;
                    }
                    if (hasChange)
                    {
                        existingAlert.LastModifiedDate = DateTime.UtcNow;
                        _context.Alerts.Update(existingAlert);

                        if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                        {
                            shouldSendNotification = true;
                            severityToSend = severity;
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            if (shouldSendNotification)
            {
                await SendLowStockNotificationAsync(snapshot, severityToSend);
            }
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "[LowStockDetection] Lỗi khi phát hiện tồn thấp cho SKU {ProductVariantId} tại kho {WarehouseId}.", productVariantId, warehouseId);
        }
    }

    private async Task SendLowStockNotificationAsync(LowStockSnapshotDto snapshot, string severity)
    {
        try
        {
            var target = new NotificationTarget
            {
                RoleIds = new List<int> { CommonConstants.Role.ADMIN }
            };

            var productNameOrSku = $"{snapshot.SKU} - {snapshot.ProductName}";
            var warehouseCodeOrName = snapshot.WarehouseCode;

            // Args khớp mẫu: "Sản phẩm \"{0}\" tại {1} còn {2} (dưới mức tối thiểu {3})."
            object[] args = new object[]
            {
                productNameOrSku,
                warehouseCodeOrName,
                $"{snapshot.AvailableKg:0.##} kg",
                $"{snapshot.ThresholdKg:0.##} kg"
            };

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.LowStockAlert,
                target,
                args,
                directionId: snapshot.ProductVariantId.ToString(),
                createdBy: CommonConstants.ADMIN_USER
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LowStockDetection] Lỗi khi gửi notification cho SKU {SKU}.", snapshot.SKU);
        }
    }

    private bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return false;
        return inner.Message.Contains("UX_Alert_DeduplicationKey") 
            || inner.Message.Contains("Duplicate entry") 
            || inner.Message.Contains("unique constraint")
            || inner.Message.Contains("constraint");
    }

    private int GetSeverityPriority(string sev)
    {
        return sev switch
        {
            AlertConstants.Severity.Critical => 3,
            AlertConstants.Severity.Warning => 2,
            _ => 1
        };
    }
}

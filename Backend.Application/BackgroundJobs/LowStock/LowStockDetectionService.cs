using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.BackgroundJobs.LowStock;

public class LowStockDetectionService : ILowStockDetectionService
{
    private readonly IApplicationDbContext _context;
    private readonly ILowStockQueryService _queryService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LowStockDetectionService> _logger;

    public LowStockDetectionService(
        IApplicationDbContext context,
        ILowStockQueryService queryService,
        INotificationDispatcher notificationDispatcher,
        ICacheService cacheService,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _queryService = queryService;
        _notificationDispatcher = notificationDispatcher;
        _cacheService = cacheService;
        _logger = loggerFactory.CreateLogger<LowStockDetectionService>();
    }

    public async Task<LowStockJobResult> DetectLowStockAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LowStockJobResult();
        var lockKey = "stocklite:job-01";

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("[LowStockDetection] Skip execution. Reason: lock_exists (Job is already running).");
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(5));

        try
        {
            // 1. Lấy toàn bộ snapshots tồn kho khả dụng của các SKU có ngưỡng
            var (snapshots, skippedCount) = await _queryService.GetLowStockSnapshotsAsync(cancellationToken);
            result.Processed = snapshots.Count;
            result.Skipped = skippedCount;

            var notificationsToSend = new List<(LowStockSnapshotDto Snapshot, string Severity, int AlertId)>();

            // 2. Duyệt qua từng record và xử lý cô lập lỗi (per-record error handling)
            foreach (var snapshot in snapshots)
            {
                using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
                    ? await _context.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                try
                {
                    // Load Alert LOW_STOCK đang hoạt động (OPEN hoặc ACKNOWLEDGED) cho SKU này tại kho này
                    var activeAlert = await _context.Alerts
                        .Where(a => a.AlertType == AlertConstants.Type.LowStock && 
                                    a.WarehouseId == snapshot.WarehouseId && 
                                    a.ProductVariantId == snapshot.ProductVariantId &&
                                    !a.IsDeleted && 
                                    a.Status != AlertConstants.Status.Resolved)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (snapshot.AvailableKg <= snapshot.ThresholdKg)
                    {
                        var severity = DetermineSeverity(snapshot.AvailableKg, snapshot.ThresholdKg);
                        var message = BuildAlertMessage(snapshot.SKU, snapshot.ProductName, snapshot.WarehouseCode, snapshot.AvailableKg, snapshot.ThresholdKg);

                        if (activeAlert == null)
                        {
                            // Chưa có Alert -> Tạo mới
                            var newAlert = CreateNewAlert(severity, message, snapshot.WarehouseId, snapshot.ProductVariantId);
                            await _context.Alerts.AddAsync(newAlert, cancellationToken);
                            await _context.SaveChangesAsync(cancellationToken);

                            result.Created++;
                            notificationsToSend.Add((snapshot, severity, newAlert.Id));
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
                                await _context.SaveChangesAsync(cancellationToken);

                                result.Updated++;

                                // Gửi notification nếu Severity tăng lên
                                if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                                {
                                    notificationsToSend.Add((snapshot, severity, activeAlert.Id));
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
                            await _context.SaveChangesAsync(cancellationToken);

                            result.Resolved++;
                        }
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                }
                catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
                {
                    // Rollback transaction cục bộ và dọn dẹp tracker
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    if (_context is DbContext dbContext)
                    {
                        dbContext.ChangeTracker.Clear();
                    }

                    _logger.LogWarning(dbEx, "[LowStockDetection] Xung đột DeduplicationKey cho SKU {ProductVariantId} tại kho {WarehouseId}. Thử nhánh cập nhật.", 
                        snapshot.ProductVariantId, snapshot.WarehouseId);

                    // Khởi động giao dịch mới để retry cập nhật
                    using var retryTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
                        ? await _context.Database.BeginTransactionAsync(cancellationToken)
                        : null;

                    try
                    {
                        var latestActiveAlert = await _context.Alerts
                            .Where(a => a.AlertType == AlertConstants.Type.LowStock && 
                                        a.WarehouseId == snapshot.WarehouseId && 
                                        a.ProductVariantId == snapshot.ProductVariantId &&
                                        !a.IsDeleted && 
                                        a.Status != AlertConstants.Status.Resolved)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (latestActiveAlert != null)
                        {
                            var severity = DetermineSeverity(snapshot.AvailableKg, snapshot.ThresholdKg);
                            var message = BuildAlertMessage(snapshot.SKU, snapshot.ProductName, snapshot.WarehouseCode, snapshot.AvailableKg, snapshot.ThresholdKg);
                            bool hasChange = false;

                            if (latestActiveAlert.Severity != severity)
                            {
                                latestActiveAlert.Severity = severity;
                                hasChange = true;
                            }
                            if (latestActiveAlert.Message != message)
                            {
                                latestActiveAlert.Message = message;
                                hasChange = true;
                            }

                            if (hasChange)
                            {
                                latestActiveAlert.LastModifiedDate = DateTime.UtcNow;
                                _context.Alerts.Update(latestActiveAlert);
                                await _context.SaveChangesAsync(cancellationToken);

                                result.Updated++;
                            }
                        }

                        if (retryTransaction != null)
                        {
                            await retryTransaction.CommitAsync(cancellationToken);
                        }
                    }
                    catch (Exception retryEx)
                    {
                        if (retryTransaction != null)
                        {
                            await retryTransaction.RollbackAsync(cancellationToken);
                        }
                        if (_context is DbContext dbCtx)
                        {
                            dbCtx.ChangeTracker.Clear();
                        }
                        _logger.LogError(retryEx, "[LowStockDetection] Lỗi khi xử lý nhánh cập nhật dự phòng cho SKU {ProductVariantId} tại kho {WarehouseId}.", 
                            snapshot.ProductVariantId, snapshot.WarehouseId);
                        result.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    // Lỗi khác: Rollback, dọn dẹp tracker và tiếp tục sang record sau
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    if (_context is DbContext dbContext)
                    {
                        dbContext.ChangeTracker.Clear();
                    }

                    _logger.LogError(ex, "[LowStockDetection] Lỗi khi xử lý tồn kho thấp cho SKU {ProductVariantId} tại kho {WarehouseId}.", 
                        snapshot.ProductVariantId, snapshot.WarehouseId);
                    result.Failed++;
                }
            }

            // 3. Gửi Notifications ngoài transaction (tránh nghẽn DB và đảm bảo tin nhắn lỗi không rollback DB)
            foreach (var (snap, sev, alertId) in notificationsToSend)
            {
                await SendLowStockNotificationAsync(snap, sev, alertId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LowStockDetection] Lỗi nghiêm trọng khi thực thi JOB-01.");
            result.Failed++;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    public async Task DetectLowStockForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken)
    {
        var lockKey = "stocklite:job-01";

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("[LowStockDetection] Skip per-product execution for SKU {ProductVariantId} at warehouse {WarehouseId}. Reason: lock_exists.", productVariantId, warehouseId);
            return;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(2));

        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // 1. Lấy snapshot tồn kho khả dụng của SKU cụ thể tại kho cụ thể
            var snapshot = await _queryService.GetLowStockSnapshotForProductAsync(warehouseId, productVariantId, cancellationToken);
            if (snapshot == null) return;

            // 2. Load Alert LOW_STOCK đang hoạt động cho SKU này
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
                var severity = DetermineSeverity(snapshot.AvailableKg, snapshot.ThresholdKg);
                var message = BuildAlertMessage(snapshot.SKU, snapshot.ProductName, snapshot.WarehouseCode, snapshot.AvailableKg, snapshot.ThresholdKg);

                if (activeAlert == null)
                {
                    var newAlert = CreateNewAlert(severity, message, snapshot.WarehouseId, snapshot.ProductVariantId);
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

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            if (shouldSendNotification)
            {
                await SendLowStockNotificationAsync(snapshot, severityToSend, activeAlert?.Id ?? 0);
            }
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            if (_context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            _logger.LogWarning(dbEx, "[LowStockDetection] Phát hiện xung đột DeduplicationKey khi thêm mới Alert cho SKU {ProductVariantId}. Thực hiện cập nhật.", productVariantId);

            using var retryTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                var existingAlert = await _context.Alerts
                    .FirstOrDefaultAsync(a =>
                        a.AlertType == AlertConstants.Type.LowStock &&
                        a.WarehouseId == warehouseId &&
                        a.ProductVariantId == productVariantId &&
                        !a.IsDeleted &&
                        a.Status != AlertConstants.Status.Resolved,
                        cancellationToken);

                var snapshot = await _queryService.GetLowStockSnapshotForProductAsync(warehouseId, productVariantId, cancellationToken);

                if (existingAlert != null && snapshot != null)
                {
                    var severity = DetermineSeverity(snapshot.AvailableKg, snapshot.ThresholdKg);
                    var message = BuildAlertMessage(snapshot.SKU, snapshot.ProductName, snapshot.WarehouseCode, snapshot.AvailableKg, snapshot.ThresholdKg);

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
                        await _context.SaveChangesAsync(cancellationToken);

                        if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                        {
                            await SendLowStockNotificationAsync(snapshot, severity, existingAlert.Id);
                        }
                    }
                }

                if (retryTransaction != null)
                {
                    await retryTransaction.CommitAsync(cancellationToken);
                }
            }
            catch (Exception retryEx)
            {
                if (retryTransaction != null)
                {
                    await retryTransaction.RollbackAsync(cancellationToken);
                }
                if (_context is DbContext dbCtx)
                {
                    dbCtx.ChangeTracker.Clear();
                }
                _logger.LogError(retryEx, "[LowStockDetection] Lỗi khi xử lý nhánh cập nhật dự phòng cho SKU {ProductVariantId} tại kho {WarehouseId}.", productVariantId, warehouseId);
            }
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            if (_context is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }
            _logger.LogError(ex, "[LowStockDetection] Lỗi khi phát hiện tồn thấp cho SKU {ProductVariantId} tại kho {WarehouseId}.", productVariantId, warehouseId);
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }
    }

    private async Task SendLowStockNotificationAsync(LowStockSnapshotDto snapshot, string severity, int alertId)
    {
        try
        {
            // Cảnh báo tồn kho thấp: gửi cho Admin, Chủ kho (theo dõi tồn) và Nhân viên thu mua (lên kế hoạch bổ sung).
            var target = new NotificationTarget
            {
                RoleIds = new List<int>
                {
                    CommonConstants.Role.ADMIN,
                    CommonConstants.Role.OWNER,
                    CommonConstants.Role.PURCHASING,
                }
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
                directionId: "/admin/alerts",
                createdBy: CommonConstants.ADMIN_USER,
                referenceType: NotificationConstants.ReferenceType.Alert,
                referenceId: alertId > 0 ? alertId : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LowStockDetection] Lỗi khi gửi notification cho SKU {SKU}.", snapshot.SKU);
        }
    }

    // L2: Tránh lặp code bằng cách dùng các hàm helper
    private string DetermineSeverity(decimal available, decimal threshold)
    {
        if (available <= 0m) return AlertConstants.Severity.Critical;
        if (available <= threshold * 0.5m) return AlertConstants.Severity.Warning;
        return AlertConstants.Severity.Info;
    }

    private string BuildAlertMessage(string sku, string productName, string warehouseCode, decimal available, decimal threshold)
    {
        return $"SKU \"{sku} - {productName}\" tại kho \"{warehouseCode}\" chỉ còn {available:0.##} kg khả dụng (dưới hoặc bằng mức tối thiểu {threshold:0.##} kg).";
    }

    private Alert CreateNewAlert(string severity, string message, int warehouseId, int productVariantId)
    {
        return new Alert
        {
            AlertType = AlertConstants.Type.LowStock,
            Severity = severity,
            WarehouseId = warehouseId,
            ProductVariantId = productVariantId,
            RelatedEntityType = AlertConstants.RelatedEntityType.ProductVariant,
            RelatedEntityId = productVariantId,
            Status = AlertConstants.Status.Open,
            Message = message,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false,
            DeduplicationKey = $"LOW_STOCK:{warehouseId}:{productVariantId}"
        };
    }

    // L3: Thu hẹp điều kiện kiểm tra trùng khoá
    private bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return false;
        
        var message = inner.Message;
        return message.Contains("UX_Alert_DeduplicationKey") 
            || message.Contains("1062") // MySQL duplicate entry error code
            || message.Contains("Duplicate entry");
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

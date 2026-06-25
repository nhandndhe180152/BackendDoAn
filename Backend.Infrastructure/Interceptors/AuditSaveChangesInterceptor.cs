using System;
using System.Collections.Generic;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Backend.Infrastructure.DependencyInjection.Extentions;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Backend.Share.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Backend.Infrastructure.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISerializeService _serializeService;
    private readonly IDataChangeNotifier _dataChangeNotifier;

    // Tên entity cần phát realtime, gom ở SavingChanges để bắn sau khi commit.
    private HashSet<string>? _pendingRealtimeNames;

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ISerializeService serializeService,
        IDataChangeNotifier dataChangeNotifier)
    {
        _httpContextAccessor = httpContextAccessor;
        _serializeService = serializeService;
        _dataChangeNotifier = dataChangeNotifier;
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries()
           .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added || e.State == EntityState.Deleted)
           .ToList();

        // Gom tên entity sẽ phát realtime (lọc theo whitelist RealtimeEntityNames).
        var realtimeNames = new HashSet<string>();
        var wroteAudit = false;

        foreach (var entry in entries)
        {
            var typeName = entry.Entity.GetType().Name;
            if (CommonConstants.RealtimeEntityNames.Contains(typeName))
                realtimeNames.Add(typeName);
            if (CommonConstants.AuditedEntityNames.Contains(typeName))
                wroteAudit = true;
        }

        // Có ghi audit log -> báo cho màn "Lịch sử thay đổi dữ liệu" làm mới.
        if (wroteAudit && CommonConstants.RealtimeEntityNames.Contains("AuditLog"))
            realtimeNames.Add("AuditLog");

        _pendingRealtimeNames = realtimeNames.Count > 0 ? realtimeNames : null;

        foreach (var entry in entries)
        {
            if (!CommonConstants.AuditedEntityNames.Contains(entry.Entity.GetType().Name))
                continue;

            var primaryKeyProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            string? targetId = null;

            if (entry.State != EntityState.Added)
            {
                targetId = primaryKeyProp?.CurrentValue?.ToString();
            }
            else
            {
                targetId = null;
            }

            var log = new AuditLog
            {
                Action = entry.State.ToString(),
                TargetType = entry.Entity.GetType().Name,
                TargetId = targetId,
                CreatedDate = DateTimeHelper.VietnamNow(),
                CreatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId(),
                IpAddress = _httpContextAccessor.HttpContext?.GetRemoteHostIpAddress(),
                UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString(),
                DataBefore = entry.State == EntityState.Modified ? SerializeValues(entry.OriginalValues) : null,
                DataAfter = SerializeValues(entry.CurrentValues),
            };
            context.Set<AuditLog>().Add(log);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Sau khi lưu thành công (đã commit) mới phát tín hiệu realtime để tránh
    /// báo nhầm khi transaction bị rollback. Lỗi realtime không làm hỏng nghiệp vụ.
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var names = _pendingRealtimeNames;
        _pendingRealtimeNames = null;

        if (names != null && names.Count > 0)
        {
            try
            {
                await _dataChangeNotifier.NotifyEntitiesChangedAsync(names);
            }
            catch
            {
                // Không để lỗi gửi realtime ảnh hưởng tới luồng lưu dữ liệu.
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private string SerializeValues(PropertyValues values)
    {
        var safeDict = values.ToSafeDictionary();
        return _serializeService.Serialize(safeDict);
    }
}

using System;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Backend.Infrastructure.DependencyInjection.Extentions;
using Backend.Share.Extensions;
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
    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor, ISerializeService serializeService)
    {
        _httpContextAccessor = httpContextAccessor;
        _serializeService = serializeService;
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries()
           .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added || e.State == EntityState.Deleted)
           .ToList();

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
                CreatedDate = DateTime.Now,
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

    private string SerializeValues(PropertyValues values)
    {
        var safeDict = values.ToSafeDictionary();
        return _serializeService.Serialize(safeDict);
    }
}

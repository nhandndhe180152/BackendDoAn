using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Alerts;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ Cảnh báo & rule-based warnings (SCR-21).
/// </summary>
public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;

    public AlertService(IAlertRepository alertRepository, ISystemConfigRepository systemConfigRepository)
    {
        _alertRepository = alertRepository;
        _systemConfigRepository = systemConfigRepository;
    }

    /// <summary>Metadata cố định của 3 quy tắc cảnh báo (nhãn + mô tả) hiển thị trên màn.</summary>
    private static readonly (string Code, string Title, string Description)[] RuleCatalog = new[]
    {
        (AlertConstants.Rules.LowStock, "Tồn kho thấp", "Khi tồn kho < ngưỡng cảnh báo"),
        (AlertConstants.Rules.WarehouseCapacity, "Kho gần đầy", "Khi sức chứa đạt ≥ 85%"),
        (AlertConstants.Rules.ExpirySoon, "Hàng sắp hết hạn", "Cảnh báo trước 7 ngày hết hạn")
    };

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _alertRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _alertRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Warehouse)
            .Include(x => x.ProductVariant)
                .ThenInclude(pv => pv!.Product)
            .Include(x => x.Location)
            .Include(x => x.AcknowledgedByUser)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetSummaryAsync()
    {
        var rows = await _alertRepository
            .GetAll(true)
            .Select(x => new { x.Status, x.Severity })
            .ToListAsync();

        var summary = new AlertSummaryDto
        {
            TotalOpen = rows.Count(x => x.Status == AlertConstants.Status.Open),
            TotalAcknowledged = rows.Count(x => x.Status == AlertConstants.Status.Acknowledged),
            TotalResolved = rows.Count(x => x.Status == AlertConstants.Status.Resolved),
            OpenCritical = rows.Count(x => x.Status == AlertConstants.Status.Open && x.Severity == AlertConstants.Severity.Critical),
            OpenWarning = rows.Count(x => x.Status == AlertConstants.Status.Open && x.Severity == AlertConstants.Severity.Warning),
            OpenInfo = rows.Count(x => x.Status == AlertConstants.Status.Open && x.Severity == AlertConstants.Severity.Info)
        };

        return ApiResponse.Success(summary);
    }

    public async Task<ApiResponse> AcknowledgeAsync(int id, int userId)
    {
        var entity = await _alertRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
            return ApiResponse.NotFound();

        if (entity.Status == AlertConstants.Status.Resolved)
            return ApiResponse.BadRequest("Cảnh báo đã được xử lý, không thể ghi nhận lại.", ApiCodeConstants.Common.BadRequest);

        entity.Status = AlertConstants.Status.Acknowledged;
        entity.AcknowledgedBy = userId;
        entity.AcknowledgedAt = DateTime.Now;
        entity.UpdatedBy = userId;
        entity.LastModifiedDate = DateTime.Now;

        await _alertRepository.UpdateAsync(entity);
        await _alertRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Đã ghi nhận cảnh báo.");
    }

    public async Task<ApiResponse> ResolveAsync(int id, int userId)
    {
        var entity = await _alertRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
            return ApiResponse.NotFound();

        if (entity.Status == AlertConstants.Status.Resolved)
            return ApiResponse.BadRequest("Cảnh báo đã được xử lý trước đó.", ApiCodeConstants.Common.BadRequest);

        entity.Status = AlertConstants.Status.Resolved;
        entity.ResolvedAt = DateTime.Now;
        if (entity.AcknowledgedBy == null)
        {
            entity.AcknowledgedBy = userId;
            entity.AcknowledgedAt = DateTime.Now;
        }
        entity.UpdatedBy = userId;
        entity.LastModifiedDate = DateTime.Now;

        await _alertRepository.UpdateAsync(entity);
        await _alertRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Đã xử lý cảnh báo.");
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _alertRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _alertRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> MarkAllReadAsync(int userId)
    {
        var openAlerts = await _alertRepository
            .FindByCondition(x => !x.IsDeleted && x.Status == AlertConstants.Status.Open)
            .ToListAsync();

        foreach (var alert in openAlerts)
        {
            alert.Status = AlertConstants.Status.Acknowledged;
            alert.AcknowledgedBy = userId;
            alert.AcknowledgedAt = DateTime.Now;
            alert.UpdatedBy = userId;
            alert.LastModifiedDate = DateTime.Now;
            await _alertRepository.UpdateAsync(alert);
        }

        await _alertRepository.SaveChangesAsync();

        return ApiResponse.Success(openAlerts.Count, "Đã đánh dấu tất cả cảnh báo là đã đọc.");
    }

    public async Task<ApiResponse> GetRulesAsync()
    {
        var stored = await _systemConfigRepository
            .FindByCondition(x => !x.IsDeleted && x.ConfigKey.StartsWith("AlertRule."))
            .ToListAsync();

        var rules = RuleCatalog
            .Select(meta =>
            {
                var key = AlertConstants.RuleEnabledKey(meta.Code);
                var config = stored.FirstOrDefault(c => c.ConfigKey == key);
                // Mặc định BẬT khi chưa có cấu hình.
                var enabled = config == null || !string.Equals(config.ConfigValue, "false", StringComparison.OrdinalIgnoreCase);
                return new AlertRuleDto
                {
                    Code = meta.Code,
                    Title = meta.Title,
                    Description = meta.Description,
                    Enabled = enabled
                };
            })
            .ToList();

        return ApiResponse.Success(rules);
    }

    public async Task<ApiResponse> ToggleRuleAsync(string code, bool enabled, int userId)
    {
        var meta = RuleCatalog.FirstOrDefault(r => r.Code == code);
        if (meta.Code == null)
            return ApiResponse.NotFound();

        var key = AlertConstants.RuleEnabledKey(code);
        var value = enabled ? "true" : "false";

        var existing = await _systemConfigRepository
            .FindByCondition(x => x.ConfigKey == key && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            var entity = new SystemConfig
            {
                ConfigKey = key,
                ConfigValue = value,
                Name = meta.Title,
                Description = meta.Description,
                UpdatedBy = userId,
                LastModifiedDate = DateTime.Now
            };
            await _systemConfigRepository.CreateAsync(entity);
        }
        else
        {
            existing.ConfigValue = value;
            existing.UpdatedBy = userId;
            existing.LastModifiedDate = DateTime.Now;
            await _systemConfigRepository.UpdateAsync(existing);
        }

        await _systemConfigRepository.SaveChangesAsync();

        return ApiResponse.Success(enabled, "Đã cập nhật quy tắc cảnh báo.");
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Alerts;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
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

    public AlertService(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

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
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ lịch hẹn thu mua lúa (PaddyPurchaseSchedule).
/// </summary>
public class PaddyPurchaseScheduleService : IPaddyPurchaseScheduleService
{
    private readonly IPaddyPurchaseScheduleRepository _scheduleRepository;
    private readonly IPaddyPurchaseReceiptRepository _receiptRepository;
    private readonly ISystemLookup _systemLookup;
    private readonly INotificationDispatcher _notificationDispatcher;

    public PaddyPurchaseScheduleService(
        IPaddyPurchaseScheduleRepository scheduleRepository,
        IPaddyPurchaseReceiptRepository receiptRepository,
        ISystemLookup systemLookup,
        INotificationDispatcher notificationDispatcher)
    {
        _scheduleRepository = scheduleRepository;
        _receiptRepository = receiptRepository;
        _systemLookup = systemLookup;
        _notificationDispatcher = notificationDispatcher;
    }

    public async Task<ApiResponse> CreateAsync(CreatePaddyPurchaseScheduleDto obj)
    {
        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var baseCode = $"SCH-{datePart}";
        var count = await _scheduleRepository.FindByCondition(x => x.ScheduleCode.StartsWith(baseCode)).CountAsync();
        var scheduleCode = $"{baseCode}-{(count + 1):D4}";

        var model = obj.ToEntity(scheduleCode);
        await _scheduleRepository.CreateAsync(model);
        await _scheduleRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên thu mua và Chủ kho về lịch thu mua mới.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.PurchaseScheduleCreated,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.PURCHASING, CommonConstants.Role.OWNER } },
            new object[] { scheduleCode },
            "/admin/rice-purchase",
            model.CreatedBy);

        return ApiResponse.Created(model.Id, "Tạo lịch thu mua thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaddyPurchaseScheduleDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        // Include đủ Giống lúa + Kho để danh sách/dropdown hiển thị được tên, không chỉ Id.
        var entities = await _scheduleRepository
            .FindByCondition(x => !x.IsDeleted, false,
                x => x.Farmer, x => x.Status, x => x.RiceVariety, x => x.Warehouse)
            .OrderByDescending(x => x.ScheduleDate)
            .ToListAsync();

        // Thống kê phiếu mua theo lịch (1 truy vấn gộp, tránh N+1) để tính cờ CanCreateReceipt.
        var scheduleIds = entities.Select(x => x.Id).ToList();
        var stats = await LoadReceiptStatsAsync(scheduleIds);

        var result = entities
            .Select(x =>
            {
                var stat = stats.GetValueOrDefault(x.Id);
                return x.ToDto(stat.Count, stat.WeightKg);
            })
            .ToList();

        return ApiResponse.Success(result);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _scheduleRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false, x => x.Farmer, x => x.Status, x => x.RiceVariety, x => x.Warehouse)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();

        var stats = await LoadReceiptStatsAsync(new List<int> { id });
        var stat = stats.GetValueOrDefault(id);

        return ApiResponse.Success(entity.ToDto(stat.Count, stat.WeightKg));
    }

    /// <summary>
    /// Gộp số phiếu mua và tổng khối lượng thực tế (kg) theo từng lịch.
    /// Chỉ tính phiếu chưa xóa — phiếu đã xóa coi như không tồn tại.
    /// </summary>
    private async Task<Dictionary<int, (int Count, decimal WeightKg)>> LoadReceiptStatsAsync(ICollection<int> scheduleIds)
    {
        if (scheduleIds.Count == 0) return new Dictionary<int, (int Count, decimal WeightKg)>();

        var rows = await _receiptRepository
            .FindByCondition(x => x.ScheduleId != null
                && scheduleIds.Contains(x.ScheduleId.Value)
                && !x.IsDeleted)
            .GroupBy(x => x.ScheduleId!.Value)
            .Select(g => new
            {
                ScheduleId = g.Key,
                Count = g.Count(),
                WeightKg = g.Sum(x => x.ActualWeightKg)
            })
            .ToListAsync();

        return rows.ToDictionary(x => x.ScheduleId, x => (x.Count, x.WeightKg));
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _scheduleRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdatePaddyPurchaseScheduleDto obj)
    {
        var existData = await _scheduleRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted) return ApiResponse.NotFound();

        obj.ToEntity(existData);
        await _scheduleRepository.UpdateAsync(existData);
        await _scheduleRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật lịch thu mua thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaddyPurchaseScheduleDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _scheduleRepository.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _scheduleRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _scheduleRepository.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _scheduleRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateStatusAsync(int id, string statusCode, int updatedBy)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        // #9: Validate statusCode hợp lệ qua ISystemLookup (Code→Id đã được cache)
        int newStatusId;
        try
        {
            newStatusId = _systemLookup.PaddyScheduleStatusId(statusCode);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse.BadRequest(message: ex.Message);
        }

        entity.StatusId = newStatusId;
        entity.UpdatedBy = updatedBy;
        entity.LastModifiedDate = DateTimeHelper.VietnamNow(); // #8

        await _scheduleRepository.UpdateAsync(entity);
        await _scheduleRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Cập nhật trạng thái thành công.");
    }
}

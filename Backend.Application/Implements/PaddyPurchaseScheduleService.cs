using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public PaddyPurchaseScheduleService(IPaddyPurchaseScheduleRepository scheduleRepository)
    {
        _scheduleRepository = scheduleRepository;
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

        return ApiResponse.Created(model.Id, "Tạo lịch thu mua thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaddyPurchaseScheduleDto> objs) => throw new NotImplementedException();

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _scheduleRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Farmer, x => x.Status)
            .OrderByDescending(x => x.ScheduleDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => x.ToDto()).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _scheduleRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false, x => x.Farmer, x => x.Status, x => x.RiceVariety)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(entity.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _scheduleRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query) => throw new NotImplementedException();
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query) => throw new NotImplementedException();

    public async Task<ApiResponse> UpdateAsync(UpdatePaddyPurchaseScheduleDto obj)
    {
        var existData = await _scheduleRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted) return ApiResponse.NotFound();

        obj.ToEntity(existData);
        await _scheduleRepository.UpdateAsync(existData);
        await _scheduleRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật lịch thu mua thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaddyPurchaseScheduleDto> objs) => throw new NotImplementedException();

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

    public async Task<ApiResponse> UpdateStatusAsync(int id, int statusId, int updatedBy)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        entity.StatusId = statusId;
        entity.UpdatedBy = updatedBy;
        entity.LastModifiedDate = DateTime.Now;

        await _scheduleRepository.UpdateAsync(entity);
        await _scheduleRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Cập nhật trạng thái thành công.");
    }
}

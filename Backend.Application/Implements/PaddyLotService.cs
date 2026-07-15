using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý Lô lúa/gạo (PaddyLot).
/// </summary>
public class PaddyLotService : IPaddyLotService
{
    private readonly IPaddyLotRepository _paddyLotRepository;

    public PaddyLotService(IPaddyLotRepository paddyLotRepository)
    {
        _paddyLotRepository = paddyLotRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreatePaddyLotDto obj)
    {
        var lotCode = await GenerateLotCodeAsync(obj.LotType);

        var model = obj.ToEntity(lotCode);

        await _paddyLotRepository.CreateAsync(model);
        await _paddyLotRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo lô thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaddyLotDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => x.ToDto()).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _paddyLotRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false,
                x => x.ProductVariant,
                x => x.Status,
                x => x.Warehouse)
            .FirstOrDefaultAsync();

        if (entity == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(entity.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _paddyLotRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdatePaddyLotDto obj)
    {
        var existData = await _paddyLotRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _paddyLotRepository.UpdateAsync(existData);
        await _paddyLotRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật lô thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaddyLotDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _paddyLotRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _paddyLotRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _paddyLotRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _paddyLotRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> GenerateLotCodeAsync(string lotType)
    {
        var prefix = lotType?.Trim().ToUpper() switch
        {
            "RICE" => "LOT-RICE",
            "BYPRODUCT" => "LOT-BP",
            _ => "LOT-PADDY"
        };

        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var baseCode = $"{prefix}-{datePart}";

        var count = await _paddyLotRepository
            .FindByCondition(x => x.LotCode.StartsWith(baseCode))
            .CountAsync();

        return $"{baseCode}-{(count + 1):D4}";
    }
}

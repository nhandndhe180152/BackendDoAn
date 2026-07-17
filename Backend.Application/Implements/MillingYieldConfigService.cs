using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ cấu hình tỷ lệ lúa→gạo (yield) theo giống lúa và dải độ ẩm (SCR-20).
/// </summary>
public class MillingYieldConfigService : IMillingYieldConfigService
{
    private readonly IMillingYieldConfigRepository _millingYieldConfigRepository;
    private readonly IRiceVarietyRepository _riceVarietyRepository;

    public MillingYieldConfigService(
        IMillingYieldConfigRepository millingYieldConfigRepository,
        IRiceVarietyRepository riceVarietyRepository)
    {
        _millingYieldConfigRepository = millingYieldConfigRepository;
        _riceVarietyRepository = riceVarietyRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateMillingYieldConfigDto obj)
    {
        if (obj.RiceVarietyId.HasValue)
        {
            var isVarietyExisting = await _riceVarietyRepository.AnyAsync(x => x.Id == obj.RiceVarietyId.Value);
            if (!isVarietyExisting)
                return ApiResponse.NotFound("Không tìm thấy giống lúa tương ứng.", ApiCodeConstants.Common.NotFound);
        }

        var isDuplicated = await _millingYieldConfigRepository.AnyAsync(x =>
            x.OrganizationId == obj.OrganizationId &&
            x.RiceVarietyId == obj.RiceVarietyId &&
            x.MoistureFrom == obj.MoistureFrom &&
            x.MoistureTo == obj.MoistureTo &&
            x.IsActive);

        if (isDuplicated)
            return ApiResponse.UnprocessableEntity(
                "Đã tồn tại cấu hình yield đang áp dụng cho cùng giống lúa và dải độ ẩm này.",
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _millingYieldConfigRepository.CreateAsync(model);
        await _millingYieldConfigRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo cấu hình yield thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateMillingYieldConfigDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _millingYieldConfigRepository
            .GetAllAsync(false, x => x.RiceVariety!);

        var data = entities
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => x.ToDto())
            .ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _millingYieldConfigRepository.GetByIdAsync(id, x => x.RiceVariety!);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _millingYieldConfigRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateMillingYieldConfigDto obj)
    {
        var existData = await _millingYieldConfigRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        if (obj.RiceVarietyId.HasValue)
        {
            var isVarietyExisting = await _riceVarietyRepository.AnyAsync(x => x.Id == obj.RiceVarietyId.Value);
            if (!isVarietyExisting)
                return ApiResponse.NotFound("Không tìm thấy giống lúa tương ứng.", ApiCodeConstants.Common.NotFound);
        }

        var isDuplicated = await _millingYieldConfigRepository.AnyAsync(x =>
            x.Id != obj.Id &&
            x.OrganizationId == obj.OrganizationId &&
            x.RiceVarietyId == obj.RiceVarietyId &&
            x.MoistureFrom == obj.MoistureFrom &&
            x.MoistureTo == obj.MoistureTo &&
            x.IsActive);

        if (isDuplicated)
            return ApiResponse.UnprocessableEntity(
                "Đã tồn tại cấu hình yield đang áp dụng cho cùng giống lúa và dải độ ẩm này.",
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _millingYieldConfigRepository.UpdateAsync(existData);
        await _millingYieldConfigRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật cấu hình yield thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateMillingYieldConfigDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _millingYieldConfigRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _millingYieldConfigRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _millingYieldConfigRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _millingYieldConfigRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

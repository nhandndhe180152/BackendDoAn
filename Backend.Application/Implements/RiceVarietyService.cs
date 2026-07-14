using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý danh mục Giống lúa (RiceVariety). Bảng phẳng — dùng DTParameter chung.
/// </summary>
public class RiceVarietyService : IRiceVarietyService
{
    private readonly IRiceVarietyRepository _riceVarietyRepository;

    public RiceVarietyService(IRiceVarietyRepository riceVarietyRepository)
    {
        _riceVarietyRepository = riceVarietyRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateRiceVarietyDto obj)
    {
        var code = obj.Code.Trim().ToLower();

        var isExistingCode = await _riceVarietyRepository.AnyAsync(
            x => x.Code.ToLower() == code && !x.IsDeleted);

        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _riceVarietyRepository.CreateAsync(model);
        await _riceVarietyRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo giống lúa thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateRiceVarietyDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _riceVarietyRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _riceVarietyRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _riceVarietyRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateRiceVarietyDto obj)
    {
        var existData = await _riceVarietyRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var code = obj.Code.Trim().ToLower();

        var isDuplicatedCode = await _riceVarietyRepository.AnyAsync(
            x => x.Code.ToLower() == code && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _riceVarietyRepository.UpdateAsync(existData);
        await _riceVarietyRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật giống lúa thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateRiceVarietyDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _riceVarietyRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _riceVarietyRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _riceVarietyRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _riceVarietyRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

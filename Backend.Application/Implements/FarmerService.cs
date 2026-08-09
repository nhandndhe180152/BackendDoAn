using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Farmers;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý danh mục Nông dân (Farmer). Bảng phẳng — dùng DTParameter chung.
/// </summary>
public class FarmerService : IFarmerService
{
    private readonly IFarmerRepository _farmerRepository;

    public FarmerService(IFarmerRepository farmerRepository)
    {
        _farmerRepository = farmerRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateFarmerDto obj)
    {
        var code = obj.Code.Trim().ToLower();

        var isExistingCode = await _farmerRepository.AnyAsync(
            x => x.Code.ToLower() == code && !x.IsDeleted);

        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        if (!string.IsNullOrWhiteSpace(obj.Phone))
        {
            var normalizedPhone = obj.Phone.Trim();
            var isDuplicatePhone = await _farmerRepository.AnyAsync(
                x => x.Phone == normalizedPhone && !x.IsDeleted);
            if (isDuplicatePhone)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Phone),
                    ApiCodeConstants.Common.DuplicatedData);
        }

        var model = obj.ToEntity();

        await _farmerRepository.CreateAsync(model);
        await _farmerRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo nông dân thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateFarmerDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _farmerRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _farmerRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _farmerRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateFarmerDto obj)
    {
        var existData = await _farmerRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var code = obj.Code.Trim().ToLower();

        var isDuplicatedCode = await _farmerRepository.AnyAsync(
            x => x.Code.ToLower() == code && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        if (!string.IsNullOrWhiteSpace(obj.Phone))
        {
            var normalizedPhone = obj.Phone.Trim();
            var isDuplicatePhone = await _farmerRepository.AnyAsync(
                x => x.Phone == normalizedPhone && x.Id != obj.Id && !x.IsDeleted);
            if (isDuplicatePhone)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Phone),
                    ApiCodeConstants.Common.DuplicatedData);
        }

        obj.ToEntity(existData);

        await _farmerRepository.UpdateAsync(existData);
        await _farmerRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật nông dân thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateFarmerDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _farmerRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _farmerRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _farmerRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _farmerRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

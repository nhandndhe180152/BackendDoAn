using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý Đơn vị tính (UnitOfMeasure). Bảng phẳng, không có quan hệ
/// join phức tạp nên dùng DTParameter chung, không cần DTParameter riêng.
/// </summary>
public class UnitOfMeasureService : IUnitOfMeasureService
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;

    public UnitOfMeasureService(IUnitOfMeasureRepository unitOfMeasureRepository)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateUnitOfMeasureDto obj)
    {
        var name = obj.Name.Trim().ToLower();
        var symbol = obj.Symbol.Trim().ToLower();

        var isExisting = await _unitOfMeasureRepository.AnyAsync(
            x => !x.IsDeleted && (x.Name.ToLower() == name || x.Symbol.ToLower() == symbol));

        if (isExisting)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _unitOfMeasureRepository.CreateAsync(model);
        await _unitOfMeasureRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo đơn vị tính thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateUnitOfMeasureDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _unitOfMeasureRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _unitOfMeasureRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _unitOfMeasureRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateUnitOfMeasureDto obj)
    {
        var existData = await _unitOfMeasureRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var name = obj.Name.Trim().ToLower();
        var symbol = obj.Symbol.Trim().ToLower();

        var isDuplicated = await _unitOfMeasureRepository.AnyAsync(
            x => x.Id != obj.Id && !x.IsDeleted && (x.Name.ToLower() == name || x.Symbol.ToLower() == symbol));

        if (isDuplicated)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _unitOfMeasureRepository.UpdateAsync(existData);
        await _unitOfMeasureRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật đơn vị tính thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUnitOfMeasureDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _unitOfMeasureRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _unitOfMeasureRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _unitOfMeasureRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _unitOfMeasureRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

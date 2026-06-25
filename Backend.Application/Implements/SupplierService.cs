using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Suppliers;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý Nhà cung cấp (Supplier). Bảng phẳng, không có quan hệ join
/// phức tạp nên dùng DTParameter chung, không cần DTParameter riêng.
/// </summary>
public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;

    public SupplierService(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateSupplierDto obj)
    {
        var code = obj.Code.Trim().ToLower();

        var isExistingCode = await _supplierRepository.AnyAsync(
            x => x.Code.ToLower() == code && !x.IsDeleted);

        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _supplierRepository.CreateAsync(model);
        await _supplierRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo nhà cung cấp thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateSupplierDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _supplierRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _supplierRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _supplierRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateSupplierDto obj)
    {
        var existData = await _supplierRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var code = obj.Code.Trim().ToLower();

        var isDuplicatedCode = await _supplierRepository.AnyAsync(
            x => x.Code.ToLower() == code && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _supplierRepository.UpdateAsync(existData);
        await _supplierRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật nhà cung cấp thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateSupplierDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _supplierRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _supplierRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _supplierRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _supplierRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

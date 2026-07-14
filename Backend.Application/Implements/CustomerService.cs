using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Customers;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý danh mục Khách hàng (Customer). Bảng phẳng — dùng DTParameter chung.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateCustomerDto obj)
    {
        var code = obj.Code.Trim().ToLower();

        var isExistingCode = await _customerRepository.AnyAsync(
            x => x.Code.ToLower() == code && !x.IsDeleted);

        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _customerRepository.CreateAsync(model);
        await _customerRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo khách hàng thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateCustomerDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _customerRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _customerRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _customerRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateCustomerDto obj)
    {
        var existData = await _customerRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var code = obj.Code.Trim().ToLower();

        var isDuplicatedCode = await _customerRepository.AnyAsync(
            x => x.Code.ToLower() == code && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _customerRepository.UpdateAsync(existData);
        await _customerRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật khách hàng thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateCustomerDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _customerRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _customerRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _customerRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _customerRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Organizations;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ quản lý Doanh nghiệp/Tenant (Organization). Bảng phẳng — dùng DTParameter chung.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationService(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateOrganizationDto obj)
    {
        var code = obj.Code.Trim().ToLower();

        var isExistingCode = await _organizationRepository.AnyAsync(
            x => x.Code.ToLower() == code && !x.IsDeleted);

        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        if (!string.IsNullOrWhiteSpace(obj.ContactPhone))
        {
            var normalizedPhone = obj.ContactPhone.Trim();
            var isDuplicatePhone = await _organizationRepository.AnyAsync(
                x => x.ContactPhone == normalizedPhone && !x.IsDeleted);
            if (isDuplicatePhone)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.ContactPhone),
                    ApiCodeConstants.Common.DuplicatedData);
        }

        var model = obj.ToEntity();

        await _organizationRepository.CreateAsync(model);
        await _organizationRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo doanh nghiệp thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateOrganizationDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _organizationRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _organizationRepository.GetByIdAsync(id);
        if (data == null || data.IsDeleted)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _organizationRepository.GetPagedAsync(parameters);
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

    public async Task<ApiResponse> UpdateAsync(UpdateOrganizationDto obj)
    {
        var existData = await _organizationRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var code = obj.Code.Trim().ToLower();

        var isDuplicatedCode = await _organizationRepository.AnyAsync(
            x => x.Code.ToLower() == code && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData);

        if (!string.IsNullOrWhiteSpace(obj.ContactPhone))
        {
            var normalizedPhone = obj.ContactPhone.Trim();
            var isDuplicatePhone = await _organizationRepository.AnyAsync(
                x => x.ContactPhone == normalizedPhone && x.Id != obj.Id && !x.IsDeleted);
            if (isDuplicatePhone)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.ContactPhone),
                    ApiCodeConstants.Common.DuplicatedData);
        }

        obj.ToEntity(existData);

        await _organizationRepository.UpdateAsync(existData);
        await _organizationRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật doanh nghiệp thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateOrganizationDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _organizationRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _organizationRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _organizationRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _organizationRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }
}

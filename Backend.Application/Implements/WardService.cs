using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Wards;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class WardService : IWardService
{
    private readonly IWardRepository _wardRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WardService(IWardRepository wardRepository, IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _wardRepository = wardRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateWardDto obj)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var model = obj.ToEntity();
        var isExistingWard = await _wardRepository.AnyAsync(
                x => x.Name.ToLower() == model.Name.ToLower() &&
                x.ProvinceId == model.ProvinceId &&
                     !x.IsDeleted);

        if (isExistingWard)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var isExistingCode = await _wardRepository.AnyAsync(
                x => x.Code.ToLower() == model.Code.ToLower() &&
                     !x.IsDeleted);
        if (isExistingCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData
            );
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
        await _wardRepository.CreateAsync(model);
        await _wardRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateWardDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _wardRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new WardDetailDto
            {
                CreatedDate = x.CreatedDate,
                Id = x.Id,
                Name = x.Name,
                ProvinceId = x.ProvinceId,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _wardRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();
        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _wardRepository.GetPagedAsync(parameters);

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

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var isDeleted = await _wardRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
        await _wardRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateWardDto obj)
    {
        var userRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = userRoleIds != null && userRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        var existData = await _wardRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();
        var isDuplicatedName = await _wardRepository.AnyAsync(
                 x => x.Name.ToLower() == obj.Name.ToLower() &&
                    x.ProvinceId == obj.ProvinceId &&
                      !x.IsDeleted && x.Id != obj.Id);

        var isDuplicatedCode = await _wardRepository.AnyAsync(
                 x => x.Code.ToLower() == obj.Code.ToLower() &&
                      !x.IsDeleted && x.Id != obj.Id);
        if (isDuplicatedCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                ApiCodeConstants.Common.DuplicatedData
            );

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        if (!isAdmin)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);

        obj.ToEntity(existData);

        await _wardRepository.UpdateAsync(existData);
        await _wardRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateWardDto> objs)
    {
        throw new NotImplementedException();
    }
}
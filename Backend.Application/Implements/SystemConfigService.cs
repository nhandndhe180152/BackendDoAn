using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly ICacheService _cacheService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public SystemConfigService(ISystemConfigRepository systemConfigRepository, IHttpContextAccessor httpContextAccessor, ICacheService cacheService)
    {
        _systemConfigRepository = systemConfigRepository;
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
    }

    public async Task<ApiResponse> CreateAsync(CreateSystemConfigDto obj)
    {
        var model = obj.ToEntity();

        var existData = await _systemConfigRepository
            .AnyAsync(x => x.ConfigKey == model.ConfigKey && !x.IsDeleted);
        if (existData)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData)
                    .Replace("{key}", model.ConfigKey),
                ApiCodeConstants.Common.DuplicatedData
            );

        await _systemConfigRepository.CreateAsync(model);
        await _systemConfigRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        var systemConfigs = await _systemConfigRepository
            .GetAllAsync();
        await _cacheService.SetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateSystemConfigDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _systemConfigRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new SystemConfigDetailDto
            {
                ConfigKey = x.ConfigKey,
                ConfigValue = x.ConfigValue,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _systemConfigRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _systemConfigRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new SystemConfigListDto
            {
                ConfigKey = x.ConfigKey,
                ConfigValue = x.ConfigValue,
                Description = x.Description,
                CreatedDate = x.CreatedDate,
                Id = x.Id,
                Name = x.Name
            });

        var totalRecord = await data.CountAsync();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                    (x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower())) ||
                    x.ConfigKey.ToLower().Contains(query.Keyword.ToLower()) ||
                    x.ConfigValue.ToLower().Contains(query.Keyword.ToLower())
                );
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data.OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var totalFiltered = await data.CountAsync();

        var pagedData = new PagingData<SystemConfigListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = totalFiltered
        };

        return ApiResponse.Success(pagedData);

    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        return await _systemConfigRepository.GetPagedAsync(parameters);
    }

    public async Task<string> GetValueByKey(string key)
    {
        return await _systemConfigRepository.GetValueByKey(key);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _systemConfigRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _systemConfigRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        var systemConfigs = await _systemConfigRepository
            .GetAllAsync();
        await _cacheService.SetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateSystemConfigDto obj)
    {
        var existData = await _systemConfigRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        var existConfigKey = await _systemConfigRepository
            .AnyAsync(x => !x.IsDeleted && x.Id != obj.Id && x.ConfigKey == existData.ConfigKey);
        if (existConfigKey)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData)
                .Replace("{key}", existData.ConfigKey),
                ApiCodeConstants.Common.DuplicatedData
            );

        await _systemConfigRepository.UpdateAsync(existData);
        await _systemConfigRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        var systemConfigs = await _systemConfigRepository
            .GetAllAsync();
        await _cacheService.SetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateSystemConfigDto> objs)
    {

        var ids = objs
            .Select(x => x.Id)
            .ToList();
        var existingData = await _systemConfigRepository
            .FindByCondition(x => ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync();

        foreach (var item in existingData)
        {
            var obj = objs
                .FirstOrDefault(x => x.Id == item.Id);
            if (obj != null)
            {
                item.ConfigValue = obj.ConfigValue;
                item.LastModifiedDate = DateTime.Now;
                item.UpdatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId();
            }
        }

        await _systemConfigRepository.UpdateListAsync(existingData);
        await _systemConfigRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        var systemConfigs = await _systemConfigRepository
            .GetAllAsync();
        await _cacheService.SetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> GetContactInformationAsync()
    {
        var configs = await _cacheService.GetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        if (configs == null)
        {
            return ApiResponse.NotFound();
        }
        var data = new ContactInformationDto
        {
            Address = configs.FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.ADDRESS_KEY))!.ConfigValue,
            Hotline = configs.FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.HOT_LINE_KEY))!.ConfigValue,
            Email = configs.FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.EMAIL_KEY))!.ConfigValue,
            WorkingHours = configs.FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.WORKING_HOURS_KEY))!.ConfigValue
        };
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPrivacyPolicyAsync()
    {
        var configs = await _cacheService.GetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        if (configs == null)
        {
            configs = await _systemConfigRepository.GetAllAsync();
        }

        var data = configs
            .FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.PRIVACY_POLIVY_KEY));

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetTermOfServiceAsync()
    {
        var configs = await _cacheService.GetAsync<List<SystemConfig>>(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY);
        if (configs == null)
        {
            configs = await _systemConfigRepository.GetAllAsync();
        }

        var data = configs
            .FirstOrDefault(x => x.ConfigKey.Equals(CommonConstants.SystemConfig.TERMS_OF_SERVICE_KEY));

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data);
    }
}

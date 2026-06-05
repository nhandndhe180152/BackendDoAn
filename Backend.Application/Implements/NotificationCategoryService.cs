using System;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class NotificationCategoryService : INotificationCategoryService
{
    private readonly INotificationCategoryRepository _notificationCategoryRepository;

    public NotificationCategoryService(INotificationCategoryRepository notificationCategoryRepository)
    {
        _notificationCategoryRepository = notificationCategoryRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateNotificationCategoryDto obj)
    {
        var model = obj.ToEntity();

        await _notificationCategoryRepository.CreateAsync(model);
        await _notificationCategoryRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateNotificationCategoryDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _notificationCategoryRepository.CreateListAsync(models);
        await _notificationCategoryRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _notificationCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new NotificationCategoryDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Color = x.Color,
                CreatedDate = x.CreatedDate,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _notificationCategoryRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _notificationCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new NotificationCategoryListDto
            {
                Color = x.Color,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<NotificationCategoryListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _notificationCategoryRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _notificationCategoryRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _notificationCategoryRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateNotificationCategoryDto obj)
    {
        var existData = await _notificationCategoryRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _notificationCategoryRepository.UpdateAsync(existData);
        await _notificationCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateNotificationCategoryDto> obj)
    {
        var listIds = obj.Select(x => x.Id).ToList();

        var existData = await _notificationCategoryRepository
            .FindByConditionAsync(x => !x.IsDeleted && listIds.Contains(x.Id));

        if (listIds.Count != existData.Count)
            return ApiResponse.BadRequest();

        foreach (var item in obj)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
                item.ToEntity(existObj);
        }

        await _notificationCategoryRepository.UpdateListAsync(existData);
        await _notificationCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}

using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Actions;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class ActionService : IActionService
{
    private readonly IActionRepository _actionRepository;

    public ActionService(IActionRepository actionRepository)
    {
        _actionRepository = actionRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateActionDto obj)
    {
        var isExistName = await _actionRepository
            .AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var model = obj.ToEntity();

        await _actionRepository.CreateAsync(model);
        await _actionRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateActionDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _actionRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ActionDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var model = await _actionRepository.GetByIdAsync(id);
        if (model == null)
            return ApiResponse.NotFound();
        var dto = model.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _actionRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ActionListDto
            {
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

        var pagedData = new PagingData<ActionListDto>
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
        var data = await _actionRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _actionRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.BadRequest),
                ApiCodeConstants.Common.BadRequest
             );

        await _actionRepository.SaveChangesAsync();

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateActionDto obj)
    {
        var isExistName = await _actionRepository
            .AnyAsync(x => !x.IsDeleted && x.Id != obj.Id && x.Name.ToLower() == obj.Name);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var existData = await _actionRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _actionRepository.UpdateAsync(existData);
        await _actionRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateActionDto> obj)
    {
        throw new NotImplementedException();
    }
}

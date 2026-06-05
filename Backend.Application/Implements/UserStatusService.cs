using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.UserStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class UserStatusService : IUserStatusService
{
    private readonly IUserStatusRepository _userStatusRepository;

    public UserStatusService(IUserStatusRepository userStatusRepository)
    {
        _userStatusRepository = userStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateUserStatusDto obj)
    {
        var isExistName = await _userStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _userStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();
        await _userStatusRepository.CreateAsync(model);
        await _userStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(model);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserStatusDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _userStatusRepository.CreateListAsync(models);
        await _userStatusRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _userStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new UserStatusListDto()
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description ?? "",
                Color = x.Color,
                CreatedDate = x.CreatedDate
            }).ToListAsync();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _userStatusRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => new UserStatusDetailDto()
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description ?? "",
                Color = x.Color,
                CreatedDate = x.CreatedDate,
            }).FirstOrDefaultAsync();
        if (data == null)
        {
            return ApiResponse.NotFound();
        }
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _userStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new UserStatusListDto
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

        var pagedData = new PagingData<UserStatusListDto>
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
        var data = await _userStatusRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _userStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _userStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _userStatusRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateUserStatusDto obj)
    {
        var isExistName = await _userStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _userStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color && x.Id != obj.Id);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existObj = await _userStatusRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existObj == null)
        {
            return ApiResponse.NotFound(obj, "Not found");
        }

        var model = obj.ToEntity(existObj);
        await _userStatusRepository.UpdateAsync(model);
        await _userStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(model);
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUserStatusDto> objs)
    {
        var listIds = objs.Select(x => x.Id).ToList();

        var existData = await _userStatusRepository
            .FindByCondition(x => listIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync();

        if (existData.Count != listIds.Count)
        {
            return ApiResponse.BadRequest();
        }

        foreach (var item in objs)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
            {
                item.ToEntity(existObj);
            }
        }

        await _userStatusRepository.UpdateListAsync(existData);
        await _userStatusRepository.SaveChangesAsync();

        return ApiResponse.Success(existData);
    }
}

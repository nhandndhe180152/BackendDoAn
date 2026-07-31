using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class ReturnToSupplierOrderStatusService : IReturnToSupplierOrderStatusService
{
    private readonly IReturnToSupplierOrderStatusRepository _returnToSupplierOrderStatusRepository;

    public ReturnToSupplierOrderStatusService(IReturnToSupplierOrderStatusRepository returnToSupplierOrderStatusRepository)
    {
        _returnToSupplierOrderStatusRepository = returnToSupplierOrderStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateReturnToSupplierOrderStatusDto obj)
    {
        var isExistName = await _returnToSupplierOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();
        await _returnToSupplierOrderStatusRepository.CreateAsync(model);
        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(model);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateReturnToSupplierOrderStatusDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());
        await _returnToSupplierOrderStatusRepository.CreateListAsync(models);
        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _returnToSupplierOrderStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ReturnToSupplierOrderStatusListDto()
            {
                Id = x.Id,
                Name = x.Name,
                Color = x.Color,
                CreatedDate = x.CreatedDate
            }).ToListAsync();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _returnToSupplierOrderStatusRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => new ReturnToSupplierOrderStatusDetailDto()
            {
                Id = x.Id,
                Name = x.Name,
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
        var data = _returnToSupplierOrderStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ReturnToSupplierOrderStatusListDto
            {
                Id = x.Id,
                Name = x.Name,
                Color = x.Color,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()));
        }

        var pagedData = new PagingData<ReturnToSupplierOrderStatusListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync<TAdv>(AdvancedSearchQuery<TAdv> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _returnToSupplierOrderStatusRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _returnToSupplierOrderStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _returnToSupplierOrderStatusRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateReturnToSupplierOrderStatusDto obj)
    {
        var isExistName = await _returnToSupplierOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existObj = await _returnToSupplierOrderStatusRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existObj == null)
        {
            return ApiResponse.NotFound(obj, "Not found");
        }

        var model = obj.ToEntity(existObj);
        await _returnToSupplierOrderStatusRepository.UpdateAsync(model);
        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(model);
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateReturnToSupplierOrderStatusDto> objs)
    {
        var listIds = objs.Select(x => x.Id).ToList();

        var existData = await _returnToSupplierOrderStatusRepository
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

        await _returnToSupplierOrderStatusRepository.UpdateListAsync(existData);
        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();

        return ApiResponse.Success(existData);
    }
}

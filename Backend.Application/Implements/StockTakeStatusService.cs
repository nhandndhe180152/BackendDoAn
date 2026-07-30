using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakeStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class StockTakeStatusService : IStockTakeStatusService
{
    private readonly IStockTakeStatusRepository _stockTakeStatusRepository;

    public StockTakeStatusService(IStockTakeStatusRepository stockTakeStatusRepository)
    {
        _stockTakeStatusRepository = stockTakeStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockTakeStatusDto obj)
    {
        var isExistName = await _stockTakeStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        if (!string.IsNullOrWhiteSpace(obj.Code))
        {
            var isExistCode = await _stockTakeStatusRepository.AnyAsync(x => !x.IsDeleted && x.Code == obj.Code);
            if (isExistCode)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                    ApiCodeConstants.Common.DuplicatedData
                );
        }
        var model = obj.ToEntity();
        await _stockTakeStatusRepository.CreateAsync(model);
        await _stockTakeStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(model);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockTakeStatusDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());
        await _stockTakeStatusRepository.CreateListAsync(models);
        await _stockTakeStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _stockTakeStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new StockTakeStatusListDto()
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Color = x.Color,
                CreatedDate = x.CreatedDate
            }).ToListAsync();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _stockTakeStatusRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => new StockTakeStatusDetailDto()
            {
                Id = x.Id,
                Code = x.Code,
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
        var data = _stockTakeStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new StockTakeStatusListDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Color = x.Color,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()));
        }

        var pagedData = new PagingData<StockTakeStatusListDto>
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
        var data = await _stockTakeStatusRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _stockTakeStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _stockTakeStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _stockTakeStatusRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateStockTakeStatusDto obj)
    {
        var isExistName = await _stockTakeStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        if (!string.IsNullOrWhiteSpace(obj.Code))
        {
            var isExistCode = await _stockTakeStatusRepository.AnyAsync(x => !x.IsDeleted && x.Code == obj.Code && x.Id != obj.Id);
            if (isExistCode)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Code),
                    ApiCodeConstants.Common.DuplicatedData
                );
        }
        var existObj = await _stockTakeStatusRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existObj == null)
        {
            return ApiResponse.NotFound(obj, "Not found");
        }

        var model = obj.ToEntity(existObj);
        await _stockTakeStatusRepository.UpdateAsync(model);
        await _stockTakeStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(model);
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockTakeStatusDto> objs)
    {
        var listIds = objs.Select(x => x.Id).ToList();

        var existData = await _stockTakeStatusRepository
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

        await _stockTakeStatusRepository.UpdateListAsync(existData);
        await _stockTakeStatusRepository.SaveChangesAsync();

        return ApiResponse.Success(existData);
    }
}

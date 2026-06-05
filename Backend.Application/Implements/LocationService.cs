using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Locations;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _locationRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public LocationService(ILocationRepository locationRepository, IWarehouseRepository warehouseRepository)
    {
        _locationRepository = locationRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateLocationDto obj)
    {
        var warehouseExists = await _warehouseRepository.AnyAsync(x => !x.IsDeleted && x.Id == obj.WarehouseId);
        if (!warehouseExists)
            return ApiResponse.BadRequest(
                message: ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.NotFound)
            );

        if (!string.IsNullOrEmpty(obj.SlotCode))
        {
            var isExistSlot = await _locationRepository.AnyAsync(x => !x.IsDeleted && x.WarehouseId == obj.WarehouseId && x.SlotCode.ToLower() == obj.SlotCode.ToLower());
            if (isExistSlot)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.SlotCode),
                    ApiCodeConstants.Common.DuplicatedData
                );
        }

        var model = obj.ToEntity();

        await _locationRepository.CreateAsync(model);
        await _locationRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateLocationDto> objs)
    {
        var models = objs.Select(x => x.ToEntity()).ToList();

        await _locationRepository.CreateListAsync(models);
        await _locationRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _locationRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Warehouse)
            .Select(x => x.ToDto())
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _locationRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, false, x => x.Warehouse);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _locationRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Warehouse)
            .Select(x => x.ToListDto());

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            data = data.Where(x => x.ZoneName.ToLower().Contains(keyword) ||
                                  x.WarehouseName.ToLower().Contains(keyword) ||
                                  (x.SlotCode != null && x.SlotCode.ToLower().Contains(keyword)));
        }

        var pagedData = new PagingData<LocationListDto>
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
        var data = await _locationRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _locationRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _locationRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateLocationDto obj)
    {
        var warehouseExists = await _warehouseRepository.AnyAsync(x => !x.IsDeleted && x.Id == obj.WarehouseId);
        if (!warehouseExists)
            return ApiResponse.BadRequest(
                message: ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.NotFound)
            );

        if (!string.IsNullOrEmpty(obj.SlotCode))
        {
            var isExistSlot = await _locationRepository.AnyAsync(x => !x.IsDeleted && x.WarehouseId == obj.WarehouseId && x.SlotCode.ToLower() == obj.SlotCode.ToLower() && x.Id != obj.Id);
            if (isExistSlot)
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.SlotCode),
                    ApiCodeConstants.Common.DuplicatedData
                );
        }

        var existData = await _locationRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _locationRepository.UpdateAsync(existData);
        await _locationRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateLocationDto> obj)
    {
        var listIds = obj.Select(x => x.Id).ToList();

        var existData = await _locationRepository
            .FindByConditionAsync(x => !x.IsDeleted && listIds.Contains(x.Id));

        if (listIds.Count != existData.Count)
            return ApiResponse.BadRequest();

        foreach (var item in obj)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
                item.ToEntity(existObj);
        }

        await _locationRepository.UpdateListAsync(existData);
        await _locationRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}

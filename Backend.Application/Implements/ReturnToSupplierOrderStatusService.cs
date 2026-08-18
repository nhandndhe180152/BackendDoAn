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
    private static readonly HashSet<string> ProtectedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReturnToSupplierOrderStatusNames.Draft,
        ReturnToSupplierOrderStatusNames.Approved,
        ReturnToSupplierOrderStatusNames.Completed,
        ReturnToSupplierOrderStatusNames.Cancelled
    };

    private readonly IReturnToSupplierOrderStatusRepository _returnToSupplierOrderStatusRepository;

    public ReturnToSupplierOrderStatusService(IReturnToSupplierOrderStatusRepository returnToSupplierOrderStatusRepository)
    {
        _returnToSupplierOrderStatusRepository = returnToSupplierOrderStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateReturnToSupplierOrderStatusDto obj)
    {
        var normalizedCode = obj.Code.Trim().ToUpperInvariant();
        var isExistCode = await _returnToSupplierOrderStatusRepository.AnyAsync(x => x.Code == normalizedCode);
        if (isExistCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", normalizedCode),
                ApiCodeConstants.Common.DuplicatedData);

        var normalizedName = obj.Name.Trim();
        var isExistName = await _returnToSupplierOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == normalizedName.ToLower());
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
        var input = objs.ToList();
        var normalizedCodes = input.Select(x => x.Code.Trim().ToUpperInvariant()).ToList();
        var normalizedNames = input.Select(x => x.Name.Trim().ToLower()).ToList();
        if (normalizedCodes.Count != normalizedCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            normalizedNames.Count != normalizedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            await _returnToSupplierOrderStatusRepository.AnyAsync(x => normalizedCodes.Contains(x.Code)) ||
            await _returnToSupplierOrderStatusRepository.AnyAsync(x => !x.IsDeleted && normalizedNames.Contains(x.Name.ToLower())))
            return ApiResponse.UnprocessableEntity("Danh sách trạng thái chứa tên hoặc mã bị trùng.", ApiCodeConstants.Common.DuplicatedData);

        var models = input.Select(x => x.ToEntity()).ToList();
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
                Code = x.Code,
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
        var data = _returnToSupplierOrderStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new ReturnToSupplierOrderStatusListDto
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
            var keyword = query.Keyword.ToLower();
            data = data.Where(x => x.Name.ToLower().Contains(keyword) || x.Code.ToLower().Contains(keyword));
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
        var status = await _returnToSupplierOrderStatusRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (status == null)
            return ApiResponse.NotFound();
        if (ProtectedCodes.Contains(status.Code))
            return ApiResponse.Conflict("Không thể xóa trạng thái hệ thống đang được nghiệp vụ sử dụng.");

        var isDeleted = await _returnToSupplierOrderStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var ids = objs.Distinct().ToList();
        var statuses = await _returnToSupplierOrderStatusRepository.FindByCondition(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync();
        if (statuses.Count != ids.Count)
            return ApiResponse.NotFound();
        if (statuses.Any(x => ProtectedCodes.Contains(x.Code)))
            return ApiResponse.Conflict("Không thể xóa trạng thái hệ thống đang được nghiệp vụ sử dụng.");

        var isDeleted = await _returnToSupplierOrderStatusRepository.SoftDeleteListAsync(ids);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        await _returnToSupplierOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateReturnToSupplierOrderStatusDto obj)
    {
        var normalizedName = obj.Name.Trim();
        var isExistName = await _returnToSupplierOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == normalizedName.ToLower() && x.Id != obj.Id);
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

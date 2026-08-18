using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.OutboundOrderStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class OutboundOrderStatusService : IOutboundOrderStatusService
{
    private static readonly HashSet<string> ProtectedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        OutboundOrderStatusNames.Draft,
        OutboundOrderStatusNames.Picking,
        OutboundOrderStatusNames.Packed,
        OutboundOrderStatusNames.Dispatched,
        OutboundOrderStatusNames.Completed,
        OutboundOrderStatusNames.Cancelled,
        OutboundOrderStatusNames.DeliveryFailed
    };

    private readonly IOutboundOrderStatusRepository _outboundOrderStatusRepository;

    public OutboundOrderStatusService(IOutboundOrderStatusRepository outboundOrderStatusRepository)
    {
        _outboundOrderStatusRepository = outboundOrderStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateOutboundOrderStatusDto obj)
    {
        var normalizedCode = obj.Code.Trim().ToUpperInvariant();
        var isExistCode = await _outboundOrderStatusRepository.AnyAsync(x => x.Code == normalizedCode);
        if (isExistCode)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", normalizedCode),
                ApiCodeConstants.Common.DuplicatedData);

        var normalizedName = obj.Name.Trim();
        var isExistName = await _outboundOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == normalizedName.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();
        await _outboundOrderStatusRepository.CreateAsync(model);
        await _outboundOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(model);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateOutboundOrderStatusDto> objs)
    {
        var input = objs.ToList();
        var normalizedCodes = input.Select(x => x.Code.Trim().ToUpperInvariant()).ToList();
        var normalizedNames = input.Select(x => x.Name.Trim().ToLower()).ToList();
        if (normalizedCodes.Count != normalizedCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            normalizedNames.Count != normalizedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            await _outboundOrderStatusRepository.AnyAsync(x => normalizedCodes.Contains(x.Code)) ||
            await _outboundOrderStatusRepository.AnyAsync(x => !x.IsDeleted && normalizedNames.Contains(x.Name.ToLower())))
            return ApiResponse.UnprocessableEntity("Danh sách trạng thái chứa tên hoặc mã bị trùng.", ApiCodeConstants.Common.DuplicatedData);

        var models = input.Select(x => x.ToEntity()).ToList();
        await _outboundOrderStatusRepository.CreateListAsync(models);
        await _outboundOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _outboundOrderStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new OutboundOrderStatusListDto()
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
        var data = await _outboundOrderStatusRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => new OutboundOrderStatusDetailDto()
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
        var data = _outboundOrderStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new OutboundOrderStatusListDto
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

        var pagedData = new PagingData<OutboundOrderStatusListDto>
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
        var data = await _outboundOrderStatusRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var status = await _outboundOrderStatusRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (status == null)
            return ApiResponse.NotFound();
        if (ProtectedCodes.Contains(status.Code))
            return ApiResponse.Conflict("Không thể xóa trạng thái hệ thống đang được nghiệp vụ sử dụng.");

        var isDeleted = await _outboundOrderStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _outboundOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var ids = objs.Distinct().ToList();
        var statuses = await _outboundOrderStatusRepository.FindByCondition(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync();
        if (statuses.Count != ids.Count)
            return ApiResponse.NotFound();
        if (statuses.Any(x => ProtectedCodes.Contains(x.Code)))
            return ApiResponse.Conflict("Không thể xóa trạng thái hệ thống đang được nghiệp vụ sử dụng.");

        var isDeleted = await _outboundOrderStatusRepository.SoftDeleteListAsync(ids);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        await _outboundOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateOutboundOrderStatusDto obj)
    {
        var normalizedName = obj.Name.Trim();
        var isExistName = await _outboundOrderStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == normalizedName.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existObj = await _outboundOrderStatusRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existObj == null)
        {
            return ApiResponse.NotFound(obj, "Not found");
        }

        var model = obj.ToEntity(existObj);
        await _outboundOrderStatusRepository.UpdateAsync(model);
        await _outboundOrderStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(model);
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateOutboundOrderStatusDto> objs)
    {
        var listIds = objs.Select(x => x.Id).ToList();

        var existData = await _outboundOrderStatusRepository
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

        await _outboundOrderStatusRepository.UpdateListAsync(existData);
        await _outboundOrderStatusRepository.SaveChangesAsync();

        return ApiResponse.Success(existData);
    }
}

using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaymentStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class PaymentStatusService : IPaymentStatusService
{
    private readonly IPaymentStatusRepository _paymentStatusRepository;
    public PaymentStatusService(IPaymentStatusRepository paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }
    public async Task<ApiResponse> CreateAsync(CreatePaymentStatusDto obj)
    {
        var isExistName = await _paymentStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _paymentStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();
        await _paymentStatusRepository.CreateAsync(model);
        await _paymentStatusRepository.SaveChangesAsync();
        return ApiResponse.Created(model);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaymentStatusDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _paymentStatusRepository.CreateListAsync(models);
        await _paymentStatusRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _paymentStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new PaymentStatusListDto()
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
        var data = await _paymentStatusRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => new PaymentStatusDetailDto()
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description ?? "",
                Color = x.Color,
                CreatedDate = x.CreatedDate
            }).FirstOrDefaultAsync();
        if (data == null)
        {
            return ApiResponse.NotFound();
        }
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _paymentStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new PaymentStatusListDto
            {
                Color = x.Color,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name
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
            data = query.SortType.ToLower().Equals("asc") ? data.OrderByDynamic(query.OrderBy, LinqExtensions.Order.Asc) : data.OrderByDynamic(query.OrderBy, LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<PaymentStatusListDto>
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
        var data = await _paymentStatusRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _paymentStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _paymentStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _paymentStatusRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> UpdateAsync(UpdatePaymentStatusDto obj)
    {
        var isExistName = await _paymentStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _paymentStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color && x.Id != obj.Id);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existObj = await _paymentStatusRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existObj == null)
        {
            return ApiResponse.NotFound(obj, "Not found");
        }

        obj.ToEntity(existObj);
        await _paymentStatusRepository.UpdateAsync(existObj);
        await _paymentStatusRepository.SaveChangesAsync();
        return ApiResponse.Success(existObj);
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaymentStatusDto> objs)
    {
        var listIds = objs.Select(x => x.Id).ToList();

        var existData = await _paymentStatusRepository
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

        await _paymentStatusRepository.UpdateListAsync(existData);
        await _paymentStatusRepository.SaveChangesAsync();

        return ApiResponse.Success(existData);
    }
}

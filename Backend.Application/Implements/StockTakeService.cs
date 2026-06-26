using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakes;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class StockTakeService : IStockTakeService
{
    private readonly IStockTakeRepository _stockTakeRepository;
    private readonly IStockTakeItemRepository _stockTakeItemRepository;
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public StockTakeService(IStockTakeRepository stockTakeRepository, IStockTakeItemRepository stockTakeItemRepository, IInventoryTransactionService inventoryTransactionService)
    {
        _stockTakeRepository = stockTakeRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockTakeDto obj)
    {
        var model = obj.ToEntity();

        await _stockTakeRepository.CreateAsync(model);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockTakeDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.StockTakeItems)
            .Select(x => x.ToDto())
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id)
                                             .Include(x => x.StockTakeItems)
                                             .FirstOrDefaultAsync();
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();
        return ApiResponse.Success(dto);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _stockTakeRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _stockTakeRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _stockTakeRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateStockTakeDto obj)
    {
        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == obj.Id)
                                                  .Include(x => x.StockTakeItems)
                                                  .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        // Handle StockTakeItems
        // 1. Delete items not in DTO
        var dtoItemIds = obj.StockTakeItems.Where(i => i.Id > 0).Select(i => i.Id).ToList();
        var itemsToDelete = existData.StockTakeItems.Where(i => !dtoItemIds.Contains(i.Id)).ToList();
        foreach (var item in itemsToDelete)
        {
            await _stockTakeItemRepository.HardDeleteAsync(item.Id);
        }

        // 2. Update existing items
        var now = DateTimeHelper.VietnamNow();
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id > 0))
        {
            var existingItem = existData.StockTakeItems.FirstOrDefault(i => i.Id == itemDto.Id);
            if (existingItem != null)
            {
                existingItem.ProductVariantId = itemDto.ProductVariantId;
                existingItem.SystemQuantity = itemDto.SystemQuantity;
                existingItem.ActualQuantity = itemDto.ActualQuantity;
                existingItem.Note = itemDto.Note;
                existingItem.QRScanned = itemDto.QRScanned;
                existingItem.LastModifiedDate = now;
            }
        }

        // 3. Add new items
        var newItems = obj.StockTakeItems.Where(i => i.Id == 0).Select(itemDto => new StockTakeItem
        {
            StockTakeId = existData.Id,
            ProductVariantId = itemDto.ProductVariantId,
            SystemQuantity = itemDto.SystemQuantity,
            ActualQuantity = itemDto.ActualQuantity,
            Note = itemDto.Note,
            QRScanned = false,
            CreatedDate = now
        }).ToList();

        if (newItems.Any())
        {
            await _stockTakeItemRepository.CreateListAsync(newItems);
        }

        await _stockTakeRepository.UpdateAsync(existData);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockTakeDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> ApproveAsync(int id, int userId)
    {
        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id)
                                                  .Include(x => x.StockTakeItems)
                                                  .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId == (int)Enums.StockTakeStatusEnum.Approved)
            return ApiResponse.UnprocessableEntity("Phiếu kiểm kho đã được duyệt.", ApiCodeConstants.Common.UnprocessableEntity);
        
        if (existData.StockTakeStatusId == (int)Enums.StockTakeStatusEnum.Rejected)
            return ApiResponse.UnprocessableEntity("Không thể duyệt phiếu đã bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);

        existData.StockTakeStatusId = (int)Enums.StockTakeStatusEnum.Approved;
        existData.ApprovedByUserId = userId;
        existData.CompletedDate = DateTimeHelper.VietnamNow();
        existData.LastModifiedDate = DateTimeHelper.VietnamNow();
        existData.UpdatedBy = userId;

        // Perform inventory adjustments
        foreach (var item in existData.StockTakeItems)
        {
            if (item.ActualQuantity.HasValue && item.Difference != 0 && item.ProductVariantId.HasValue)
            {
                var request = new DTOs.InventoryTransactions.StockMovementRequestDto
                {
                    ProductVariantId = item.ProductVariantId.Value,
                    WarehouseId = existData.WarehouseId,
                    Quantity = Math.Abs(item.Difference),
                    ReferenceType = "STOCKTAKE",
                    ReferenceId = existData.Id,
                    ReferenceItemId = item.Id,
                    Note = $"Điều chỉnh kiểm kho {existData.STCode}"
                };

                await _inventoryTransactionService.AdjustStockAsync(request, item.ActualQuantity.Value);
            }
        }

        await _stockTakeRepository.UpdateAsync(existData);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> RejectAsync(int id, string reason, int userId)
    {
        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId == (int)Enums.StockTakeStatusEnum.Approved)
            return ApiResponse.UnprocessableEntity("Không thể từ chối phiếu kiểm kho đã duyệt.", ApiCodeConstants.Common.UnprocessableEntity);
            
        if (existData.StockTakeStatusId == (int)Enums.StockTakeStatusEnum.Rejected)
            return ApiResponse.UnprocessableEntity("Phiếu kiểm kho đã bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);

        existData.StockTakeStatusId = (int)Enums.StockTakeStatusEnum.Rejected;
        existData.Note = string.IsNullOrWhiteSpace(existData.Note) ? $"Từ chối: {reason}" : $"{existData.Note} | Từ chối: {reason}";
        existData.LastModifiedDate = DateTimeHelper.VietnamNow();
        existData.UpdatedBy = userId;

        await _stockTakeRepository.UpdateAsync(existData);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}

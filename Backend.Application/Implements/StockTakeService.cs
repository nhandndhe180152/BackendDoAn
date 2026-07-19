using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Common;
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
using Backend.Share.Extensions;

namespace Backend.Application.Implements;

public class StockTakeService : IStockTakeService
{
    private readonly IStockTakeRepository _stockTakeRepository;
    private readonly IStockTakeItemRepository _stockTakeItemRepository;
    private readonly IInventoryTransactionService _inventoryTransactionService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public StockTakeService(
        IStockTakeRepository stockTakeRepository, 
        IStockTakeItemRepository stockTakeItemRepository, 
        IInventoryTransactionService inventoryTransactionService,
        IInventoryRepository inventoryRepository,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _stockTakeRepository = stockTakeRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _inventoryTransactionService = inventoryTransactionService;
        _inventoryRepository = inventoryRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockTakeDto obj)
    {
        var model = obj.ToEntity();
        model.StartedDate = DateTimeHelper.VietnamNow();

        foreach (var item in model.StockTakeItems)
        {
            if (item.ProductVariantId.HasValue)
            {
                // SỬA LỖI BLOCKING-1: Tính tồn hệ thống (SystemQuantity) cho nghiệp vụ kiểm kê
                // - Thay vì lấy tồn kho dòng đơn bằng GetByVariantWarehouseLocationAsync (chỉ khớp dòng có PaddyLotId = null và trả về 0 cho hàng có lô),
                //   hệ thống sẽ lấy tổng tồn (Sum) của VARIANT đó ở tất cả các LÔ khác nhau đang nằm tại cùng warehouse + location.
                // - Việc này đảm bảo tính đúng đắn số lượng tồn thực tế của lúa/gạo khi lập phiếu kiểm kê.
                item.SystemQuantity = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == item.ProductVariantId.Value &&
                        x.WarehouseId == model.WarehouseId &&
                        x.LocationId == item.LocationId)
                    .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
            }
        }

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
        var existData = await _stockTakeRepository.FindByCondition(x => x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();
            
        if (existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) || 
            existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể xóa phiếu đã duyệt hoặc bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);
        }

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

        if (existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) || 
            existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể cập nhật phiếu đã duyệt hoặc bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) || 
            obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể cập nhật trạng thái Duyệt/Từ chối qua chức năng này.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted) && 
            existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft))
        {
            return ApiResponse.UnprocessableEntity("Chỉ có thể Gửi duyệt phiếu đang ở trạng thái Nháp.", ApiCodeConstants.Common.UnprocessableEntity);
        }

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
                existingItem.ActualQuantity = itemDto.ActualQuantity;
                existingItem.Note = itemDto.Note;
                existingItem.QRScanned = itemDto.QRScanned;
                existingItem.LastModifiedDate = now;
            }
        }

        // 3. Add new items
        var newItems = new List<StockTakeItem>();
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id == 0))
        {
            decimal sysQty = 0;
            if (itemDto.ProductVariantId.HasValue)
            {
                // SỬA LỖI BLOCKING-1: Tính tồn hệ thống (SystemQuantity) cho dòng kiểm kê mới được thêm vào
                // - Lấy tổng tồn (Sum) của variant trên tất cả các lô khác nhau để đảm bảo hiển thị đúng số tồn
                //   cho hàng hóa theo lô lúa/gạo.
                sysQty = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == itemDto.ProductVariantId.Value &&
                        x.WarehouseId == existData.WarehouseId &&
                        x.LocationId == itemDto.LocationId)
                    .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
            }

            newItems.Add(new StockTakeItem
            {
                StockTakeId = existData.Id,
                ProductVariantId = itemDto.ProductVariantId,
                LocationId = itemDto.LocationId,
                SystemQuantity = sysQty,
                ActualQuantity = itemDto.ActualQuantity,
                Note = itemDto.Note,
                QRScanned = itemDto.QRScanned,
                CreatedDate = now
            });
        }

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

    public async Task<ApiResponse> ApproveAsync(int id, string? approveNote, int userId)
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? new List<int>();
        if (!currentRoleIds.Contains(CommonConstants.Role.ADMIN))
        {
            return ApiResponse.Forbidden();
        }

        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id)
                                                  .Include(x => x.StockTakeItems)
                                                  .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
        {
            return ApiResponse.UnprocessableEntity("Chỉ có thể duyệt phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved);
            existData.ApprovedByUserId = userId;
            existData.ApproveNote = approveNote;
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
                        LocationId = item.LocationId,
                        Quantity = Math.Abs(item.Difference),
                        ReferenceType = "STOCKTAKE",
                        ReferenceId = existData.Id,
                        ReferenceItemId = item.Id,
                        Note = $"Điều chỉnh kiểm kho {existData.STCode}"
                    };

                    var result = await _inventoryTransactionService.AdjustStockAsync(request, item.ActualQuantity.Value, true);
                    if (!result.IsSucceeded)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse.UnprocessableEntity($"Lỗi điều chỉnh tồn kho: {result.Message}", ApiCodeConstants.Common.UnprocessableEntity);
                    }
                }
            }

            await _stockTakeRepository.UpdateAsync(existData);
            await _stockTakeRepository.SaveChangesAsync();
            
            await transaction.CommitAsync();
            return ApiResponse.Success();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse> RejectAsync(int id, string reason, int userId)
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? new List<int>();
        if (!currentRoleIds.Contains(CommonConstants.Role.ADMIN))
        {
            return ApiResponse.Forbidden();
        }

        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
        {
            return ApiResponse.UnprocessableEntity("Chỉ có thể từ chối phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected);
            existData.Note = string.IsNullOrWhiteSpace(existData.Note) ? $"Từ chối: {reason}" : $"{existData.Note} | Từ chối: {reason}";
            existData.LastModifiedDate = DateTimeHelper.VietnamNow();
            existData.UpdatedBy = userId;

            await _stockTakeRepository.UpdateAsync(existData);
            await _stockTakeRepository.SaveChangesAsync();
            
            await transaction.CommitAsync();
            return ApiResponse.Success();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

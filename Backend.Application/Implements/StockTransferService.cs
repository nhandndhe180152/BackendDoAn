using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTransfers;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ phiếu điều chuyển nội bộ (StockTransfer).
/// ConfirmTransferAsync: xuất kho nguồn → nhập kho đích → cập nhật PaddyLot.WarehouseId.
/// </summary>
public class StockTransferService : IStockTransferService
{
    private readonly IStockTransferRepository _transferRepository;
    private readonly IRepositoryBase<StockTransferItem, int> _itemRepository;
    private readonly IRepositoryBase<StockTransferStatus, int> _statusRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;

    public StockTransferService(
        IStockTransferRepository transferRepository,
        IRepositoryBase<StockTransferItem, int> itemRepository,
        IRepositoryBase<StockTransferStatus, int> statusRepository,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository)
    {
        _transferRepository = transferRepository;
        _itemRepository = itemRepository;
        _statusRepository = statusRepository;
        _paddyLotRepository = paddyLotRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockTransferDto obj)
    {
        if (obj.FromWarehouseId == obj.ToWarehouseId)
            return ApiResponse.BadRequest(message: "Kho nguồn và kho đích không được trùng nhau.");

        var now = DateTimeHelper.VietnamNow();
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"ST-{datePart}";
        var count = await _transferRepository
            .FindByCondition(x => x.TransferCode.StartsWith(baseCode))
            .CountAsync();
        var transferCode = $"{baseCode}-{(count + 1):D4}";

        var pendingStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Pending" && !x.IsDeleted)
            ?? await _statusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy StockTransferStatus.");

        var transfer = new StockTransfer
        {
            OrganizationId = obj.OrganizationId,
            TransferCode = transferCode,
            StatusId = pendingStatus.Id,
            FromWarehouseId = obj.FromWarehouseId,
            ToWarehouseId = obj.ToWarehouseId,
            TransferDate = obj.TransferDate,
            AssignedUserId = obj.AssignedUserId,
            Note = obj.Note?.Trim(),
            CreatedBy = obj.CreatedBy,
            CreatedDate = now
        };

        await _transferRepository.CreateAsync(transfer);
        await _transferRepository.SaveChangesAsync();

        foreach (var itemDto in obj.Items)
        {
            var item = new StockTransferItem
            {
                StockTransferId = transfer.Id,
                ProductVariantId = itemDto.ProductVariantId,
                PaddyLotId = itemDto.PaddyLotId,
                FromLocationId = itemDto.FromLocationId,
                ToLocationId = itemDto.ToLocationId,
                WeightKg = itemDto.WeightKg,
                Note = itemDto.Note?.Trim(),
                CreatedBy = obj.CreatedBy,
                CreatedDate = now
            };
            await _itemRepository.CreateAsync(item);
        }

        await _transferRepository.SaveChangesAsync();

        return ApiResponse.Created(transfer.Id, "Tạo phiếu điều chuyển thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockTransferDto> objs) => throw new NotImplementedException();

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _transferRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Status, x => x.FromWarehouse, x => x.ToWarehouse)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _transferRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false,
                x => x.Status,
                x => x.FromWarehouse,
                x => x.ToWarehouse,
                x => x.StockTransferItems)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDto(entity));
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _transferRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query) => throw new NotImplementedException();
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query) => throw new NotImplementedException();

    public async Task<ApiResponse> UpdateAsync(UpdateStockTransferDto obj)
    {
        var entity = await _transferRepository.GetByIdAsync(obj.Id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Completed" && !x.IsDeleted);
        if (completedStatus != null && entity.StatusId == completedStatus.Id)
            return ApiResponse.UnprocessableEntity("Phiếu điều chuyển đã hoàn thành, không thể chỉnh sửa.");

        entity.OrganizationId = obj.OrganizationId;
        entity.FromWarehouseId = obj.FromWarehouseId;
        entity.ToWarehouseId = obj.ToWarehouseId;
        entity.TransferDate = obj.TransferDate;
        entity.AssignedUserId = obj.AssignedUserId;
        entity.Note = obj.Note?.Trim();
        entity.UpdatedBy = obj.UpdatedBy;
        entity.LastModifiedDate = DateTime.Now;

        await _transferRepository.UpdateAsync(entity);
        await _transferRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Cập nhật phiếu điều chuyển thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockTransferDto> objs) => throw new NotImplementedException();

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _transferRepository.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _transferRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _transferRepository.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _transferRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    /// <summary>
    /// Xác nhận điều chuyển:
    /// 1. Với mỗi item: EXPORT từ FromWarehouse, IMPORT vào ToWarehouse
    /// 2. Nếu PaddyLotId được set: cập nhật PaddyLot.WarehouseId = ToWarehouseId
    /// 3. Status → Completed
    /// </summary>
    public async Task<ApiResponse> ConfirmTransferAsync(int id, int confirmedById)
    {
        var transfer = await _transferRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.StockTransferItems)
            .FirstOrDefaultAsync();

        if (transfer == null) return ApiResponse.NotFound();

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Completed" && !x.IsDeleted);
        if (completedStatus != null && transfer.StatusId == completedStatus.Id)
            return ApiResponse.UnprocessableEntity("Phiếu đã xác nhận trước đó.");

        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _transferRepository.BeginTransactionAsync();
        try
        {
            foreach (var item in transfer.StockTransferItems)
            {
                decimal costPrice = 0;
                PaddyLot? lot = null;

                if (item.PaddyLotId.HasValue)
                {
                    lot = await _paddyLotRepository.GetByIdAsync(item.PaddyLotId.Value);
                    if (lot != null && !lot.IsDeleted)
                    {
                        costPrice = lot.CostPricePerKg;
                    }
                }
                else
                {
                    var sourceInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                        item.ProductVariantId, transfer.FromWarehouseId, item.FromLocationId);
                    costPrice = sourceInventory?.CostPrice ?? 0;
                }

                // EXPORT từ FromWarehouse (sẽ tự động ném Exception nếu không đủ tồn kho)
                await MoveInventoryAsync(
                    item.ProductVariantId, transfer.FromWarehouseId, item.FromLocationId,
                    item.WeightKg, isExport: true, costPrice,
                    refId: id, refItemId: item.Id, userId: confirmedById, now,
                    note: $"Điều chuyển từ kho {transfer.FromWarehouseId} → {transfer.ToWarehouseId}");

                // IMPORT vào ToWarehouse
                await MoveInventoryAsync(
                    item.ProductVariantId, transfer.ToWarehouseId, item.ToLocationId,
                    item.WeightKg, isExport: false, costPrice,
                    refId: id, refItemId: item.Id, userId: confirmedById, now,
                    note: $"Nhận hàng điều chuyển từ kho {transfer.FromWarehouseId}");

                // Cập nhật lô lúa (Nếu chuyển 1 phần thì tách lô để bảo toàn vị trí phần còn lại)
                if (lot != null)
                {
                    if (item.WeightKg < lot.RemainingWeightKg)
                    {
                        // Tách lô mới ở kho đích
                        var datePart = now.ToString("yyyyMMdd");
                        var lotType = lot.LotType;
                        var baseCode = $"LOT-{lotType}-{datePart}";
                        var count = await _paddyLotRepository.FindByCondition(x => x.LotCode.StartsWith(baseCode)).CountAsync();
                        var newLotCode = $"{baseCode}-{(count + 1):D4}";

                        var newLot = new PaddyLot
                        {
                            OrganizationId = lot.OrganizationId,
                            LotCode = newLotCode,
                            LotType = lot.LotType,
                            ProductVariantId = lot.ProductVariantId,
                            RiceVarietyId = lot.RiceVarietyId,
                            StatusId = lot.StatusId,
                            SourceReceiptId = lot.SourceReceiptId,
                            SourceMillingOrderId = lot.SourceMillingOrderId,
                            WarehouseId = transfer.ToWarehouseId,
                            LocationId = item.ToLocationId,
                            InboundDate = lot.InboundDate,
                            InitialWeightKg = item.WeightKg,
                            RemainingWeightKg = item.WeightKg,
                            CostPricePerKg = lot.CostPricePerKg,
                            QualityStatus = lot.QualityStatus,
                            CreatedBy = confirmedById,
                            CreatedDate = now
                        };
                        await _paddyLotRepository.CreateAsync(newLot);
                        await _paddyLotRepository.SaveChangesAsync();

                        // Trừ RemainingWeightKg của lô gốc
                        lot.RemainingWeightKg -= item.WeightKg;
                        lot.LastModifiedDate = now;
                        await _paddyLotRepository.UpdateAsync(lot);
                        await _paddyLotRepository.SaveChangesAsync();
                    }
                    else
                    {
                        // Chuyển toàn bộ lô
                        lot.WarehouseId = transfer.ToWarehouseId;
                        lot.LocationId = item.ToLocationId;
                        lot.LastModifiedDate = now;
                        await _paddyLotRepository.UpdateAsync(lot);
                        await _paddyLotRepository.SaveChangesAsync();
                    }
                }
            }

            // Update status
            if (completedStatus != null)
                transfer.StatusId = completedStatus.Id;

            transfer.UpdatedBy = confirmedById;
            transfer.LastModifiedDate = now;
            await _transferRepository.UpdateAsync(transfer);
            await _transferRepository.SaveChangesAsync();

            await _transferRepository.EndTransactionAsync();

            return ApiResponse.Success(new { TransferId = id }, "Xác nhận điều chuyển thành công. Đã cập nhật tồn kho 2 kho.");
        }
        catch (InvalidOperationException ex)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch
        {
            await _transferRepository.RollbackTransactionAsync();
            throw;
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task MoveInventoryAsync(
        int productVariantId, int warehouseId, int? locationId,
        decimal qty, bool isExport, decimal costPrice,
        int refId, int refItemId, int userId, DateTime now, string note)
    {
        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
            productVariantId, warehouseId, locationId);

        if (inventory == null)
        {
            if (isExport)
            {
                throw new InvalidOperationException($"Không có tồn kho của sản phẩm tại kho {warehouseId} (kệ {locationId}) để xuất điều chuyển {qty} kg.");
            }

            inventory = new Inventory
            {
                WarehouseId = warehouseId,
                LocationId = locationId,
                ProductVariantId = productVariantId,
                CostPrice = costPrice,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                CreatedDate = now
            };
            await _inventoryRepository.CreateAsync(inventory);
            await _inventoryRepository.SaveChangesAsync();
        }

        var before = inventory.QuantityOnHand;

        if (isExport)
        {
            if (inventory.QuantityOnHand < qty)
            {
                throw new InvalidOperationException($"Số lượng tồn kho tại kho {warehouseId} (kệ {locationId}) không đủ (chỉ còn {inventory.QuantityOnHand} kg) để xuất điều chuyển {qty} kg.");
            }

            inventory.QuantityOnHand = Math.Max(0, inventory.QuantityOnHand - qty);
        }
        else
        {
            // Áp dụng bình quân gia quyền cho giá vốn khi nhập hàng điều chuyển vào kho đích
            if (before > 0 && inventory.CostPrice > 0)
            {
                inventory.CostPrice = Math.Round(((before * inventory.CostPrice) + (qty * costPrice)) / (before + qty), 2);
            }
            else
            {
                inventory.CostPrice = costPrice;
            }

            inventory.QuantityOnHand += qty;
        }

        inventory.LastModifiedDate = now;
        await _inventoryRepository.UpdateAsync(inventory);

        var tx = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            WarehouseId = warehouseId,
            LocationId = locationId,
            ProductVariantId = productVariantId,
            TransactionType = isExport
                ? InventoryTransactionTypeConstants.Export
                : InventoryTransactionTypeConstants.Import,
            ReferenceType = InventoryReferenceTypeConstants.StockTransfer,
            ReferenceId = refId,
            ReferenceItemId = refItemId,
            Quantity = isExport ? -qty : qty,
            BeforeQuantity = before,
            AfterQuantity = inventory.QuantityOnHand,
            WeightKg = qty,
            Note = note,
            CreatedDate = now,
            CreatedBy = userId
        };

        await _inventoryTransactionRepository.CreateAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
    }

    private static StockTransferDetailDto ToDto(StockTransfer x) => new()
    {
        Id = x.Id,
        OrganizationId = x.OrganizationId,
        TransferCode = x.TransferCode,
        StatusId = x.StatusId,
        StatusName = x.Status?.Name,
        FromWarehouseId = x.FromWarehouseId,
        FromWarehouseName = x.FromWarehouse?.Name,
        ToWarehouseId = x.ToWarehouseId,
        ToWarehouseName = x.ToWarehouse?.Name,
        AssignedUserId = x.AssignedUserId,
        TransferDate = x.TransferDate,
        Note = x.Note,
        Items = x.StockTransferItems.Select(i => new StockTransferItemDetailDto
        {
            Id = i.Id,
            ProductVariantId = i.ProductVariantId,
            SKU = i.ProductVariant?.SKU,
            ProductVariantName = i.ProductVariant?.Name,
            PaddyLotId = i.PaddyLotId,
            LotCode = i.PaddyLot?.LotCode,
            FromLocationId = i.FromLocationId,
            ToLocationId = i.ToLocationId,
            WeightKg = i.WeightKg,
            Note = i.Note
        }).ToList(),
        CreatedDate = x.CreatedDate,
        LastModifiedDate = x.LastModifiedDate
    };
}

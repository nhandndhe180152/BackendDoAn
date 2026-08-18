using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IInventoryRepository : IRepositoryBase<Inventory, int>
{
    Task<DTResult<InventoryAggregate>> GetPagedAsync(InventoryDTParameters parameters);

    /// <summary>Tổng hợp tồn kho theo trạng thái cho 5 thẻ KPI (giám sát tồn kho).</summary>
    Task<InventoryStockSummaryAggregate> GetStockSummaryAsync(InventorySummaryParameters parameters);

    Task<Inventory?> GetByIdDetailAsync(int id);

    Task<Inventory?> GetByVariantWarehouseLocationAsync(int productVariantId, int warehouseId, int? locationId, int? paddyLotId = null);

    Task<List<Inventory>> GetByProductVariantAsync(int productVariantId);

    Task<List<Inventory>> GetLowStockAsync(int? warehouseId, int limit = 50);

    /// <summary>
    /// Trả về danh sách Inventory (theo lot) của warehouse có tồn khả dụng > 0,
    /// lot không bị cách ly và IsSellable = true.
    /// Dùng cho bước ReserveAsync để chọn lô.
    /// </summary>
    Task<List<Inventory>> GetAvailableForSalesAsync(int productVariantId, int warehouseId);
}

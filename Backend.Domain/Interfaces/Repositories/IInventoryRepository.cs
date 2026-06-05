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

    Task<Inventory?> GetByIdDetailAsync(int id);

    Task<Inventory?> GetByVariantWarehouseLocationAsync(int productVariantId, int warehouseId, int? locationId);

    Task<List<Inventory>> GetByProductVariantAsync(int productVariantId);

    Task<List<Inventory>> GetLowStockAsync(int? warehouseId, int limit = 50);
}

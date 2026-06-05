using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IInventoryTransactionRepository : IRepositoryBase<InventoryTransaction, int>
{
    Task<DTResult<InventoryTransactionAggregate>> GetPagedAsync(InventoryTransactionDTParameters parameters);

    Task<InventoryTransaction?> GetByIdDetailAsync(int id);

    Task<List<InventoryTransaction>> GetByProductVariantAsync(int productVariantId, int limit = 100);
}

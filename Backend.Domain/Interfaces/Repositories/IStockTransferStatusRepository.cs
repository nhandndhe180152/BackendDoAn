using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IStockTransferStatusRepository : IRepositoryBase<StockTransferStatus, int>
{
    Task<DTResult<StockTransferStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

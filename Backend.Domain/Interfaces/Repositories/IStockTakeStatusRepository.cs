using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IStockTakeStatusRepository : IRepositoryBase<StockTakeStatus, int>
{
    Task<DTResult<StockTakeStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IStockTakeRepository : IRepositoryBase<StockTake, int>
{
    Task<DTResult<StockTakeAggregate>> GetPagedAsync(DTParameter parameters);
}

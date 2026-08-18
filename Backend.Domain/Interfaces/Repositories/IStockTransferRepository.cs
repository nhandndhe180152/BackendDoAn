using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IStockTransferRepository : IRepositoryBase<StockTransfer, int>
{
    Task<DTResult<StockTransferAggregate>> GetPagedAsync(DTParameter parameters);
}

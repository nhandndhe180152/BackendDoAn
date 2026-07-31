using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ISalesOrderStatusRepository : IRepositoryBase<SalesOrderStatus, int>
{
    Task<DTResult<SalesOrderStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

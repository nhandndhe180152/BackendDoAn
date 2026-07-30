using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IMillingOrderStatusRepository : IRepositoryBase<MillingOrderStatus, int>
{
    Task<DTResult<MillingOrderStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

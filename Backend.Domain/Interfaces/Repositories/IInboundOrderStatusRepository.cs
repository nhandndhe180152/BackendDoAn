using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IInboundOrderStatusRepository : IRepositoryBase<InboundOrderStatus, int>
{
    Task<DTResult<InboundOrderStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

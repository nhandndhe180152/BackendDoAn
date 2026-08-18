using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IPurchaseOrderStatusRepository : IRepositoryBase<PurchaseOrderStatus, int>
{
    Task<DTResult<PurchaseOrderStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ICustomerReturnOrderStatusRepository : IRepositoryBase<CustomerReturnOrderStatus, int>
{
    Task<DTResult<CustomerReturnOrderStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IPaddyPurchaseScheduleStatusRepository : IRepositoryBase<PaddyPurchaseScheduleStatus, int>
{
    Task<DTResult<PaddyPurchaseScheduleStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

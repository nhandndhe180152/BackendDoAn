using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ILotStatusRepository : IRepositoryBase<LotStatus, int>
{
    Task<DTResult<LotStatusAggregate>> GetPagedAsync(DTParameter parameters);
}

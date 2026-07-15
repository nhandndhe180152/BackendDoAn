using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IMillingOrderRepository : IRepositoryBase<MillingOrder, int>
{
    Task<DTResult<MillingOrderAggregate>> GetPagedAsync(DTParameter parameters);
}

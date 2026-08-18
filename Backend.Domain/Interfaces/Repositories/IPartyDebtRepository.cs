using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IPartyDebtRepository : IRepositoryBase<PartyDebt, int>
{
    Task<DTResult<PartyDebtAggregate>> GetPagedAsync(DTParameter parameters);
}

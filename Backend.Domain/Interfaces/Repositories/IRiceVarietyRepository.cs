using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IRiceVarietyRepository : IRepositoryBase<RiceVariety, int>
{
    Task<DTResult<RiceVarietyAggregate>> GetPagedAsync(DTParameter parameters);
}

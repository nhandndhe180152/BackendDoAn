using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IActionRepository : IRepositoryBase<Entities.Action, int>
{
    Task<DTResult<ActionAggregate>> GetPagedAsync(DTParameter parameters);
}

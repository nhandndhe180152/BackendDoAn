using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IWardRepository : IRepositoryBase<Ward, int>
{
    Task<DTResult<WardAggregate>> GetPagedAsync(DTParameter parameters);
}

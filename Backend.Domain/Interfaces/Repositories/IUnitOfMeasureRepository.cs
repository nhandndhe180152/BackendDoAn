using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IUnitOfMeasureRepository : IRepositoryBase<UnitOfMeasure, int>
{
    Task<DTResult<UnitOfMeasureAggregate>> GetPagedAsync(DTParameter parameters);
}

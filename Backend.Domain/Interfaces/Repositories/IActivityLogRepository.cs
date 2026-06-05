using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IActivityLogRepository : IRepositoryBase<ActivityLog, int>
{
    Task<DTResult<ActivityLogAggregate>> GetPagedAsync(ActivityLogDTParameters parameters);
}

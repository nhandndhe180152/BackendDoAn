using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IUserDeviceRepository : IRepositoryBase<UserDevice, int>
{
    Task<DTResult<UserDeviceAggregate>> GetPagedAsync(DTParameter parameters);
    Task MarkTokensAsDeletedAsync(List<string> deviceTokens);
}

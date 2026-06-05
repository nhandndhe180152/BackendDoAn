using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IIotDeviceRepository : IRepositoryBase<IotDevice, int>
{
    Task<DTResult<IotDeviceAggregate>> GetPagedAsync(DTParameter parameters);
    Task<IotDeviceAggregate?> GetDetailByIdAsync(int id);
    Task<IotDevice?> GetByDeviceCodeAsync(string deviceCode, bool trackChanges = false);
    Task<IotDevice?> GetActiveByDeviceCodeAsync(string deviceCode);
}


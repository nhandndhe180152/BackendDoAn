using System;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IIotDeviceCommandRepository
{
    Task<DTResult<IotDeviceCommandAggregate>> GetPagedAsync(DTParameter parameters);
    Task<IotDeviceCommandAggregate?> GetDetailByIdAsync(int id);
    Task<IotDeviceCommand?> GetNextPendingCommandAsync(int iotDeviceId, bool trackChanges = true);
    Task<IotDeviceCommand?> GetCommandWithDeviceAsync(int commandId, bool trackChanges = true);
}

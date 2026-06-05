using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IIotWeightLogRepository : IRepositoryBase<IotWeightLog, int>
{
    Task<IotWeightLog?> GetLatestByDeviceIdAsync(int iotDeviceId);

    Task<IotWeightLog?> GetByIdForAttachAsync(int id);
}

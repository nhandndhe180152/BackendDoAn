using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ISystemConfigRepository : IRepositoryBase<SystemConfig, int>
{
    public Task<ApiResponse> GetPagedAsync(DTParameter parameters);
    Task<string> GetValueByKey(string key);
}

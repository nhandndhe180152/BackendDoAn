using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IUserRepository : IRepositoryBase<User, int>
{
    Task<DTResult<UserAggregates>> GetPagedAsync(UserDTParameters parameters);
    Task<List<MenuAggregate>> GetMenuAsync(int userId);
    Task<List<PermissionAggregate>> GetPermissionsAsync(int userId);
}

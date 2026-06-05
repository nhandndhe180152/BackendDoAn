using System;
using Backend.Application.DTOs.UserRoles;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserRoleService : IServiceBase<int, CreateUserRoleDto, UpdateUserRoleDto, DTParameter>
{
}

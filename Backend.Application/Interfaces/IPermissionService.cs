using System;
using Backend.Application.DTOs.Permissions;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPermissionService : IServiceBase<int, CreatePermissionDto, UpdatePermissionDto, DTParameter>
{
}

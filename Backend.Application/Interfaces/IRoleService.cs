using System;
using Backend.Application.DTOs.Permissions;
using Backend.Application.DTOs.Roles;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IRoleService : IServiceBase<int, CreateRoleDto, UpdateRoleDto, DTParameter>
{
    Task<ApiResponse> GetPermissionAsync(int roleId);
    Task<ApiResponse> UpdatePermissionAsync(int roleId, List<UpdatePermissionDto> objs);
    Task<ApiResponse> GetByAdminRegister();
    Task<ApiResponse> GetListRoleForUserManagementAsync();

}

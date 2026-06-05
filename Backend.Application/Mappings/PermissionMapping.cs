using System;
using Backend.Application.DTOs.Permissions;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PermissionMapping
{
    public static Permission ToEntity(this CreatePermissionDto dto)
    {
        return new Permission
        {
            ActionId = dto.ActionId,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            MenuId = dto.MenuId,
            RoleId = dto.RoleId,
        };
    }

    public static Permission ToEntity(this UpdatePermissionDto dto)
    {
        return new Permission
        {
            ActionId = dto.ActionId,
            CreatedBy = dto.UpdatedBy,
            CreatedDate = DateTime.Now,
            UpdatedBy = dto.UpdatedBy,
            LastModifiedDate = DateTime.Now,
            MenuId = dto.MenuId,
            RoleId = dto.RoleId,
        };
    }
}

using System;

namespace Backend.Application.DTOs.Permissions;

public class CreatePermissionDto
{
    public int RoleId { get; set; } = 0;
    public int MenuId { get; set; } = 0;
    public int ActionId { get; set; } = 0;
    public int? CreatedBy { get; set; }
}

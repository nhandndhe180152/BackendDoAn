using System;

namespace Backend.Application.DTOs.Permissions;

public class PermissionListDto
{
    public int Id { get; set; }
    public int ActionId { get; set; }
    public string ActionName { get; set; } = null!;
    public int MenuId { get; set; }
    public string MenuName { get; set; } = null!;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;
}

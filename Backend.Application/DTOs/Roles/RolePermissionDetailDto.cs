using System;

namespace Backend.Application.DTOs.Roles;

public class RolePermissionDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<RoleMenuActionDto> Permissions { get; set; } = new List<RoleMenuActionDto>();
}

using System;

namespace Backend.Application.DTOs.Roles;

public class CreateRoleDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
    public bool IsCheckAll { get; set; }
    public List<RoleMenuActionDto> Permissions { get; set; } = new List<RoleMenuActionDto>();
}

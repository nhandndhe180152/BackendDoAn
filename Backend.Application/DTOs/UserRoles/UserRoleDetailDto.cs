using System;

namespace Backend.Application.DTOs.UserRoles;

public class UserRoleDetailDto
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;
}

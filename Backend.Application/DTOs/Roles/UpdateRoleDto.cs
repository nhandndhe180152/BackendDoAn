using System;

namespace Backend.Application.DTOs.Roles;

public class UpdateRoleDto : CreateRoleDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}

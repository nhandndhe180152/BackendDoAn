using System;

namespace Backend.Application.DTOs.Roles;

public class RoleListDto : RoleDetailDto
{
    public int TotalUser { get; set; } = 0;
    public List<RolePermissonDto> Permissons { get; set; } = new List<RolePermissonDto>();
}
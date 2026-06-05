using System;

namespace Backend.Application.DTOs.Roles;

public class RolePermissonDto
{
    public int Id { get; set; }
    public int ActionId { get; set; }
    public string ActionName { get; set; } = null!;
    public int MenuId { get; set; }
    public string MenuName { get; set; } = null!;
}

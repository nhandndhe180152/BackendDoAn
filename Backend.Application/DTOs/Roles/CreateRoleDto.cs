using System;

namespace Backend.Application.DTOs.Roles;

public class CreateRoleDto
{
    /// <summary>Mã định danh ổn định (tùy chọn) — chỉ cần khi code backend tham chiếu role này theo Code (vd ADMIN).</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
    public bool IsCheckAll { get; set; }
    public List<RoleMenuActionDto> Permissions { get; set; } = new List<RoleMenuActionDto>();
}

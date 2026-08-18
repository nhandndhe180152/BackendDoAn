using System;

namespace Backend.Application.DTOs.UserStatuses;

public class CreateUserStatusDto
{
    /// <summary>Mã định danh ổn định dùng trong code (vd Actived/Locked). Không phải tên hiển thị.</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; } = null!;
    public string Color { get; set; }
    public int CreatedBy { get; set; }
}

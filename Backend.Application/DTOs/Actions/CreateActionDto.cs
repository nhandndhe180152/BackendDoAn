using System;

namespace Backend.Application.DTOs.Actions;

public class CreateActionDto
{
    /// <summary>Mã định danh ổn định (bất biến) — dùng cho phân quyền/so sánh trong code, không phải tên hiển thị.</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
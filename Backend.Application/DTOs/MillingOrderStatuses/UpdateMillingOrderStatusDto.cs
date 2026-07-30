using System;

namespace Backend.Application.DTOs.MillingOrderStatuses;

public class UpdateMillingOrderStatusDto
{
    public int Id { get; set; }
    /// <summary>Mã định danh ổn định dùng trong code (không phải tên hiển thị).</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? UpdatedBy { get; set; }
}

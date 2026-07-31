using System;

namespace Backend.Application.DTOs.LotStatuses;

public class CreateLotStatusDto
{
    /// <summary>Mã định danh ổn định dùng trong code (không phải tên hiển thị).</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool IsSellable { get; set; } = true;
    public int? CreatedBy { get; set; }
}

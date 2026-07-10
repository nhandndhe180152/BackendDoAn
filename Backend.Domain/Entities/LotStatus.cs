using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái lô lúa/gạo trong kho.
/// IsSellable=false khoá lô khỏi xuất/bán/xay (Cách ly, Chờ xử lý).
/// </summary>
public class LotStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool IsSellable { get; set; } = true;
}

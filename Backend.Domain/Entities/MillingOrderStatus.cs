using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái lệnh xay xát (seed data).
/// </summary>
public class MillingOrderStatus : EntityAuditBase<int>
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
}

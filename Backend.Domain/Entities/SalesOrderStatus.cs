using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái đơn bán offline (seed data).
/// </summary>
public class SalesOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Color { get; set; } = null!;
}

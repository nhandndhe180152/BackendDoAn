using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái lịch thu mua lúa (seed data).
/// </summary>
public class PaddyPurchaseScheduleStatus : EntityAuditBase<int>
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
}

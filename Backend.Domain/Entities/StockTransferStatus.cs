using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái phiếu điều chuyển nội bộ (seed data).
/// </summary>
public class StockTransferStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
}

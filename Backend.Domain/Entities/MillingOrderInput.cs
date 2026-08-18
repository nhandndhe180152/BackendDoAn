using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Dòng lô/cột lúa đầu vào của lệnh xay.
/// Cho phép một mẻ lấy từ nhiều cột.
/// </summary>
public class MillingOrderInput : EntityAuditBase<int>
{
    public int MillingOrderId { get; set; }
    public int PaddyLotId { get; set; }
    public int? LocationId { get; set; }
    public decimal? ReservedWeightKg { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual MillingOrder MillingOrder { get; set; } = null!;
    public virtual PaddyLot PaddyLot { get; set; } = null!;
    public virtual Location? Location { get; set; }
}

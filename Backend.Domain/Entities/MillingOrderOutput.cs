using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Dòng đầu ra của lệnh xay: gạo thành phẩm và/hoặc phụ phẩm (tấm/cám/trấu).
/// Mỗi dòng nhập kho theo ProductVariant/SKU.
/// </summary>
public class MillingOrderOutput : EntityAuditBase<int>
{
    public int MillingOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public int? OutputLotId { get; set; }
    public int? LocationId { get; set; }

    /// <summary>RICE | BROKEN | BRAN | HUSK</summary>
    public string OutputType { get; set; } = null!;

    public int? BagCount { get; set; }
    public decimal OutputWeightKg { get; set; }
    public bool IsByproduct { get; set; }
    public decimal? UnitCost { get; set; }

    // Navigation
    public virtual MillingOrder MillingOrder { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual PaddyLot? OutputLot { get; set; }
    public virtual Location? Location { get; set; }
}

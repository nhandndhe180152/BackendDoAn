using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Dòng chi tiết đơn mua.
/// InboundOrderItem tham chiếu về đây để đối chiếu số lượng.
/// </summary>
public class PurchaseOrderItem : EntityAuditBase<int>
{
    public int PurchaseOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal LineAmount { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
}

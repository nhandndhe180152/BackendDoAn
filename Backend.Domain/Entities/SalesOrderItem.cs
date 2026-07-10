using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Dòng chi tiết đơn bán.
/// OutboundOrderItem tham chiếu về đây để đối chiếu số lượng.
/// </summary>
public class SalesOrderItem : EntityAuditBase<int>
{
    public int SalesOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal UnitSalePrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal LineAmount { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
}

using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Chi tiết phân bổ lô và vị trí trả hàng cho từng dòng mặt hàng trả (FE-16).
/// Liên kết chi tiết dòng trả hàng của khách với phân bổ Picking của Outbound gốc.
/// </summary>
public class CustomerReturnOrderItemAllocation : EntityAuditBase<int>
{
    public int CustomerReturnOrderItemId { get; set; }
    public int? OutboundOrderItemAllocationId { get; set; }
    public int PaddyLotId { get; set; }
    public int ProductVariantId { get; set; }
    public int? OriginalLocationId { get; set; }
    
    public decimal QuantityReturned { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public decimal QuantityRejected { get; set; }
    public decimal CreditQuantity { get; set; }
    
    public int? RestockLocationId { get; set; }
    public int? QuarantineLocationId { get; set; }
    
    public decimal UnitCreditPrice { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual CustomerReturnOrderItem CustomerReturnOrderItem { get; set; } = null!;
    public virtual OutboundOrderItemAllocation? OutboundOrderItemAllocation { get; set; }
    public virtual PaddyLot PaddyLot { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual Location? OriginalLocation { get; set; }
    public virtual Location? RestockLocation { get; set; }
    public virtual Location? QuarantineLocation { get; set; }
}

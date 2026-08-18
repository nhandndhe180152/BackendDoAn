using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Chi tiết mặt hàng trong đơn hoàn hàng từ khách (FE-16)
/// </summary>
public class CustomerReturnOrderItem : EntityAuditBase<int>
{
    public int CustomerReturnOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public string QualityStatus { get; set; } = null!; // GOOD | DAMAGED | EXPIRED
    public string? DamageReason { get; set; }
    public int? RestockLocationId { get; set; }
    public int? QuarantineLocationId { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    public virtual CustomerReturnOrder CustomerReturnOrder { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? RestockLocation { get; set; }
    public virtual Location? QuarantineLocation { get; set; }
    public virtual ICollection<CustomerReturnOrderItemAllocation> Allocations { get; set; } = new List<CustomerReturnOrderItemAllocation>();
}

using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Chi tiết mặt hàng trong đơn trả về nhà cung cấp (FE-16)
/// </summary>
public class ReturnToSupplierOrderItem : EntityAuditBase<int>
{
    public int ReturnToSupplierOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? QuarantineLocationId { get; set; }
    public int QuantityToReturn { get; set; }
    public int QuantityActualReturned { get; set; }
    public string? DamageReason { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    public virtual ReturnToSupplierOrder ReturnToSupplierOrder { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? QuarantineLocation { get; set; }
}

using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class InboundOrderItem : EntityAuditBase<int>
{
    public int InboundOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal? ExpectedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    public virtual InboundOrder InboundOrder { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
}

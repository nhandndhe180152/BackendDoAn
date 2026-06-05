using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class OutboundOrderItem : EntityAuditBase<int>
{
    public int OutboundOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityPicked { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal? ExpectedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    public virtual OutboundOrder OutboundOrder { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
}

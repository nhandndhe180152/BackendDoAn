using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class InboundOrderItem : EntityAuditBase<int>
{
    public int InboundOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal? ExpectedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    /// <summary>Liên kết dòng đơn mua nguồn — đối chiếu số lượng</summary>
    public int? PurchaseOrderItemId { get; set; }

    /// <summary>Lô lúa được tạo khi nhập</summary>
    public int? PaddyLotId { get; set; }

    public virtual InboundOrder InboundOrder { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }
    public virtual PaddyLot? PaddyLot { get; set; }
}


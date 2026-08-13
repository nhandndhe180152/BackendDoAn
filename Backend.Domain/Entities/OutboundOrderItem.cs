using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class OutboundOrderItem : EntityAuditBase<int>
{
    public int OutboundOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityPicked { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal? ExpectedWeightKg { get; set; }

    /// <summary>
    /// Khối lượng đóng gói thực tế của dòng này (ghi ở bước confirm-packing).
    /// </summary>
    public decimal? ActualWeightKg { get; set; }

    /// <summary>
    /// Nguồn của <see cref="ActualWeightKg"/>: "SCALE" (cân điện tử) hoặc
    /// "MANUAL" (nhập tay). NULL khi chưa cân.
    /// </summary>
    public string? ActualWeightSource { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    /// <summary>Liên kết dòng đơn bán nguồn — dùng để đối chiếu số lượng</summary>
    public int? SalesOrderItemId { get; set; }

    public virtual OutboundOrder OutboundOrder { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual SalesOrderItem? SalesOrderItem { get; set; }
    public virtual ICollection<OutboundOrderItemAllocation> Allocations { get; set; } = new List<OutboundOrderItemAllocation>();
}


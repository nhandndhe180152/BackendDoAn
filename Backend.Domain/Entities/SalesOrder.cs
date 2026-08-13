using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Đơn bán — chứng từ đứng TRƯỚC OutboundOrder.
/// Một SalesOrder có thể sinh 1..n OutboundOrder (giao/xuất từng phần).
/// Kênh chỉ Offline (DIRECT / WHOLESALE).
/// </summary>
public class SalesOrder : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string SOCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public int StatusId { get; set; }

    /// <summary>DIRECT (khách gọi) | WHOLESALE (nhà buôn theo lịch)</summary>
    public string Channel { get; set; } = null!;

    public int? WarehouseId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Đơn cần xay mới có gạo giao</summary>
    public bool RequiresMilling { get; set; }

    public string? ShippingAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Note { get; set; }

    /// <summary>Lý do hủy đơn — chỉ có giá trị khi đơn ở trạng thái CANCELLED.</summary>
    public string? CancelReason { get; set; }

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual Customer Customer { get; set; } = null!;
    public virtual SalesOrderStatus Status { get; set; } = null!;
    public virtual Warehouse? Warehouse { get; set; }
    public virtual ICollection<SalesOrderItem> SalesOrderItems { get; set; } = new List<SalesOrderItem>();
    public virtual ICollection<OutboundOrder> OutboundOrders { get; set; } = new List<OutboundOrder>();
    public virtual ICollection<MillingOrder> MillingOrders { get; set; } = new List<MillingOrder>();
}

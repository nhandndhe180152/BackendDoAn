using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Đơn mua — chứng từ đứng TRƯỚC InboundOrder.
/// Dùng khi nhập hàng qua đơn mua (VD mua gạo/vật tư).
/// Song song với luồng thu mua lúa trực tiếp (PaddyPurchaseReceipt).
/// </summary>
public class PurchaseOrder : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string POCode { get; set; } = null!;
    public int SupplierId { get; set; }
    public int StatusId { get; set; }
    public int? WarehouseId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual PurchaseOrderStatus Status { get; set; } = null!;
    public virtual Warehouse? Warehouse { get; set; }
    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public virtual ICollection<InboundOrder> InboundOrders { get; set; } = new List<InboundOrder>();
}

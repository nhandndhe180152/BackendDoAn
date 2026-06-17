using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Đơn trả hàng về nhà cung cấp (FE-16)
/// </summary>
public class ReturnToSupplierOrder : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int SupplierId { get; set; }
    public int ReturnToSupplierOrderStatusId { get; set; }
    public int? InboundOrderId { get; set; }
    public string ReturnCode { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? CompletedDate { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual ReturnToSupplierOrderStatus ReturnToSupplierOrderStatus { get; set; } = null!;
    public virtual InboundOrder? InboundOrder { get; set; }
    public virtual User? ApprovedByUser { get; set; }
    public virtual ICollection<ReturnToSupplierOrderItem> Items { get; set; } = new List<ReturnToSupplierOrderItem>();
}

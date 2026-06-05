using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PurchaseOrder : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public int PurchaseOrderStatusId { get; set; }
    public string POCode { get; set; } = null!;
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? DeliveryNoteId { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Supplier? Supplier { get; set; }
    public virtual PurchaseOrderStatus PurchaseOrderStatus { get; set; } = null!;
    public virtual DeliveryNote? DeliveryNote { get; set; }
    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
}

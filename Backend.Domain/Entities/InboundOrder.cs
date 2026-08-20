using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class InboundOrder : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public int InboundOrderStatusId { get; set; }

    /// <summary>Fallback khi nhập không qua chứng từ; nullable sau khi có PurchaseOrderId</summary>
    public string? POCode { get; set; }

    public string? Note { get; set; }
    public decimal TotalAssetValue { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? DeliveryNoteId { get; set; }

    /// <summary>Nguồn gốc phiếu nhập khi mua qua PO</summary>
    public int? PurchaseOrderId { get; set; }

    /// <summary>Nguồn gốc khi nhập lúa thu mua trực tiếp</summary>
    public int? PaddyPurchaseReceiptId { get; set; }

    /// <summary>Loại nguồn: RECEIPT | PO | MANUAL | STOCK_TRANSFER</summary>
    public string? SourceType { get; set; }

    /// <summary>Nguồn gốc khi phiếu nhập được sinh tự động từ chuyển kho nội bộ (SourceType = STOCK_TRANSFER).
    /// Dùng để truy vết ngược về phiếu chuyển và kho nguồn.</summary>
    public int? StockTransferId { get; set; }

    /// <summary>Multi-tenant (nullable ở MVP)</summary>
    public int? OrganizationId { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Supplier? Supplier { get; set; }
    public virtual InboundOrderStatus InboundOrderStatus { get; set; } = null!;
    public virtual DeliveryNote? DeliveryNote { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
    public virtual PaddyPurchaseReceipt? PaddyPurchaseReceipt { get; set; }
    public virtual StockTransfer? StockTransfer { get; set; }
    public virtual Organization? Organization { get; set; }
    public virtual ICollection<InboundOrderItem> InboundOrderItems { get; set; } = new List<InboundOrderItem>();
}


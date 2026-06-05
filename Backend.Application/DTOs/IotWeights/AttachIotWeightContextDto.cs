using System;

namespace Backend.Application.DTOs.IotWeights;

public class AttachIotWeightContextDto
{
    /// <summary>
    /// SKU/ProductVariant đang được cân.
    /// Bắt buộc với PURCHASE_ORDER, SALES_ORDER, STOCK_TAKE.
    /// Có thể null với MANUAL.
    /// </summary>
    public int? ProductVariantId { get; set; }

    /// <summary>
    /// PURCHASE_ORDER, SALES_ORDER, STOCK_TAKE, MANUAL.
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// Id phiếu chính.
    /// Ví dụ:
    /// - PurchaseOrder.Id
    /// - SalesOrder.Id
    /// - StockTake.Id
    /// </summary>
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Id dòng item trong phiếu.
    /// Ví dụ:
    /// - PurchaseOrderItem.Id
    /// - SalesOrderItem.Id
    /// - StockTakeItem.Id
    /// </summary>
    public int? ReferenceItemId { get; set; }

    /// <summary>
    /// Nếu true:
    /// - PURCHASE_ORDER: update PurchaseOrderItem.ActualWeightKg
    /// - SALES_ORDER: update SalesOrderItem.ActualWeightKg
    /// STOCK_TAKE hiện chưa có ActualWeightKg nên không update.
    /// </summary>
    public bool UpdateReferenceItemActualWeight { get; set; } = true;

    public string? Note { get; set; }
}

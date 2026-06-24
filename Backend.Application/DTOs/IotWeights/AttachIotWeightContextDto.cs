using System;

namespace Backend.Application.DTOs.IotWeights;

public class AttachIotWeightContextDto
{
    /// <summary>
    /// SKU/ProductVariant đang được cân.
    /// Bắt buộc với INBOUND_ORDER, OUTBOUND_ORDER, STOCK_TAKE.
    /// Có thể null với MANUAL.
    /// </summary>
    public int? ProductVariantId { get; set; }

    /// <summary>
    /// INBOUND_ORDER, OUTBOUND_ORDER, STOCK_TAKE, MANUAL.
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// Id phiếu chính.
    /// Ví dụ:
    /// - InboundOrder.Id
    /// - OutboundOrder.Id
    /// - StockTake.Id
    /// </summary>
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Id dòng item trong phiếu.
    /// Ví dụ:
    /// - InboundOrderItem.Id
    /// - OutboundOrderItem.Id
    /// - StockTakeItem.Id
    /// </summary>
    public int? ReferenceItemId { get; set; }

    /// <summary>
    /// Nếu true:
    /// - INBOUND_ORDER: update InboundOrderItem.ActualWeightKg
    /// - OUTBOUND_ORDER: update OutboundOrderItem.ActualWeightKg
    /// STOCK_TAKE hiện chưa có ActualWeightKg nên không update.
    /// </summary>
    public bool UpdateReferenceItemActualWeight { get; set; } = true;

    public string? Note { get; set; }
}

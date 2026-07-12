using System;

namespace Backend.Application.Constants;

public static class IotWeightReferenceTypeConstants
{
    // ── Cũ ──────────────────────────────────────────
    public const string InboundOrder  = "INBOUND_ORDER";
    public const string OutboundOrder = "OUTBOUND_ORDER";
    public const string StockTake     = "STOCK_TAKE";
    public const string Manual        = "MANUAL";

    // ── Mới — Chuỗi cung ứng lúa/gạo (spec mục 7) ──
    public const string PaddyPurchase = "PADDY_PURCHASE";  // cân lúa tại điểm thu mua
    public const string MillingOrder  = "MILLING_ORDER";   // cân lúa vào / gạo ra khi xay
    public const string StockTransfer = "STOCK_TRANSFER";  // cân khi điều chuyển nội bộ

    public static readonly string[] All =
    [
        InboundOrder,
        OutboundOrder,
        StockTake,
        Manual,
        PaddyPurchase,
        MillingOrder,
        StockTransfer
    ];

    public static string Normalize(string? referenceType)
    {
        if (string.IsNullOrWhiteSpace(referenceType))
        {
            return string.Empty;
        }

        var value = referenceType.Trim()
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToUpperInvariant();

        return value switch
        {
            "PO" or "PURCHASEORDER" or "PURCHASE_ORDER" or "IO" or "INBOUNDORDER" or "INBOUND_ORDER" => InboundOrder,
            "SO" or "SALESORDER" or "SALES_ORDER" or "OO" or "OUTBOUNDORDER" or "OUTBOUND_ORDER"     => OutboundOrder,
            "ST" or "STOCKTAKE" or "STOCK_TAKE"                                                       => StockTake,
            "MANUAL"                                                                                  => Manual,
            "PADDY_PURCHASE" or "PADDY" or "PADDYPURCHASE"                                           => PaddyPurchase,
            "MILLING_ORDER" or "MILLING" or "MILLINGORDER"                                           => MillingOrder,
            "STOCK_TRANSFER" or "TRANSFER" or "STOCKTRANSFER"                                        => StockTransfer,
            _ => value
        };
    }

    public static bool IsValid(string? referenceType)
    {
        var normalized = Normalize(referenceType);
        return All.Contains(normalized);
    }

    public static bool RequiresReferenceItem(string? referenceType)
    {
        var normalized = Normalize(referenceType);
        return normalized is InboundOrder or OutboundOrder or StockTake or PaddyPurchase or MillingOrder;
    }
}

using System;

namespace Backend.Application.Constants;

public static class InventoryReferenceTypeConstants
{
    // ── Cũ ──────────────────────────────────────────
    public const string InboundOrder   = "INBOUND_ORDER";
    public const string OutboundOrder  = "OUTBOUND_ORDER";
    public const string StockTake      = "STOCK_TAKE";
    public const string Manual         = "MANUAL";
    public const string OpeningBalance = "OPENING_BALANCE";

    // ── Mới — Chuỗi cung ứng lúa/gạo (spec mục 7) ──
    public const string PaddyPurchase  = "PADDY_PURCHASE";   // nhập lúa từ phiếu mua lúa
    public const string MillingOrder   = "MILLING_ORDER";    // trừ lúa / nhập gạo & phụ phẩm từ lệnh xay
    public const string SalesOrder     = "SALES_ORDER";      // đối chiếu tồn theo đơn bán
    public const string PurchaseOrder  = "PURCHASE_ORDER";   // đối chiếu tồn theo đơn mua (non-paddy)
    public const string StockTransfer  = "STOCK_TRANSFER";   // điều chuyển nội bộ giữa kho

    public static readonly string[] All =
    [
        InboundOrder,
        OutboundOrder,
        StockTake,
        Manual,
        OpeningBalance,
        PaddyPurchase,
        MillingOrder,
        SalesOrder,
        PurchaseOrder,
        StockTransfer
    ];

    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return Manual;
        }

        var value = type.Trim()
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToUpperInvariant();

        return value switch
        {
            "PO" or "PURCHASEORDER" or "PURCHASE_ORDER" or "IO" or "INBOUNDORDER" or "INBOUND_ORDER" => InboundOrder,
            "SO" or "SALESORDER" or "SALES_ORDER" or "OO" or "OUTBOUNDORDER" or "OUTBOUND_ORDER"     => OutboundOrder,
            "ST" or "STOCKTAKE" or "STOCK_TAKE"                                                       => StockTake,
            "OPENING" or "OPENING_BALANCE"                                                            => OpeningBalance,
            "MANUAL"                                                                                  => Manual,
            "PADDY_PURCHASE" or "PADDY" or "PADDYPURCHASE"                                           => PaddyPurchase,
            "MILLING_ORDER" or "MILLING" or "MILLINGORDER"                                           => MillingOrder,
            "SALES_ORDER" or "SALESORDER_NEW"                                                        => SalesOrder,
            "PURCHASE_ORDER" or "PURCHASEORDER_NEW"                                                  => PurchaseOrder,
            "STOCK_TRANSFER" or "TRANSFER" or "STOCKTRANSFER"                                        => StockTransfer,
            _ => value
        };
    }

    public static bool IsValid(string? type)
    {
        var normalized = Normalize(type);
        return All.Contains(normalized);
    }
}

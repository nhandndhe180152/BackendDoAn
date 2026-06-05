using System;

namespace Backend.Application.Constants;

public static class InventoryReferenceTypeConstants
{
    public const string InboundOrder = "INBOUND_ORDER";
    public const string OutboundOrder = "OUTBOUND_ORDER";
    public const string StockTake = "STOCK_TAKE";
    public const string Manual = "MANUAL";
    public const string OpeningBalance = "OPENING_BALANCE";

    public static readonly string[] All =
    [
        InboundOrder,
        OutboundOrder,
        StockTake,
        Manual,
        OpeningBalance
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
            "SO" or "SALESORDER" or "SALES_ORDER" or "OO" or "OUTBOUNDORDER" or "OUTBOUND_ORDER" => OutboundOrder,
            "ST" or "STOCKTAKE" or "STOCK_TAKE" => StockTake,
            "OPENING" or "OPENING_BALANCE" => OpeningBalance,
            "MANUAL" => Manual,
            _ => value
        };
    }

    public static bool IsValid(string? type)
    {
        var normalized = Normalize(type);
        return All.Contains(normalized);
    }
}

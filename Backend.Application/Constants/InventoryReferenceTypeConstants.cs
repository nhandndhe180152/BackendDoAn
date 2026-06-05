using System;

namespace Backend.Application.Constants;

public static class InventoryReferenceTypeConstants
{
    public const string PurchaseOrder = "PURCHASE_ORDER";
    public const string SalesOrder = "SALES_ORDER";
    public const string StockTake = "STOCK_TAKE";
    public const string Manual = "MANUAL";
    public const string OpeningBalance = "OPENING_BALANCE";

    public static readonly string[] All =
    [
        PurchaseOrder,
        SalesOrder,
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
            "PO" or "PURCHASEORDER" or "PURCHASE_ORDER" => PurchaseOrder,
            "SO" or "SALESORDER" or "SALES_ORDER" => SalesOrder,
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

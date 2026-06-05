using System;

namespace Backend.Application.Constants;

public static class IotWeightReferenceTypeConstants
{
    public const string PurchaseOrder = "PURCHASE_ORDER";
    public const string SalesOrder = "SALES_ORDER";
    public const string StockTake = "STOCK_TAKE";
    public const string Manual = "MANUAL";

    public static readonly string[] All =
    [
        PurchaseOrder,
        SalesOrder,
        StockTake,
        Manual
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
            "PO" or "PURCHASEORDER" or "PURCHASE_ORDER" => PurchaseOrder,
            "SO" or "SALESORDER" or "SALES_ORDER" => SalesOrder,
            "ST" or "STOCKTAKE" or "STOCK_TAKE" => StockTake,
            "MANUAL" => Manual,
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
        return normalized is PurchaseOrder or SalesOrder or StockTake;
    }
}

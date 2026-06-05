using System;

namespace Backend.Application.Constants;

public static class InventoryTransactionTypeConstants
{
    public const string Import = "IMPORT";
    public const string Export = "EXPORT";
    public const string StockTakeAdjust = "STOCKTAKE_ADJUST";
    public const string ManualAdjust = "MANUAL_ADJUST";
    public const string Reserve = "RESERVE";
    public const string ReleaseReserve = "RELEASE_RESERVE";

    public static readonly string[] All =
    [
        Import,
        Export,
        StockTakeAdjust,
        ManualAdjust,
        Reserve,
        ReleaseReserve
    ];

    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        var value = type.Trim()
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToUpperInvariant();

        return value switch
        {
            "IN" or "INBOUND" or "RECEIVE" or "RECEIVING" or "IMPORT" => Import,
            "OUT" or "OUTBOUND" or "DISPATCH" or "EXPORT" => Export,
            "STOCK_TAKE" or "STOCKTAKE" or "STOCKTAKE_ADJUST" => StockTakeAdjust,
            "MANUAL" or "MANUAL_ADJUST" or "ADJUST" => ManualAdjust,
            "RESERVE" => Reserve,
            "RELEASE_RESERVE" or "UNRESERVE" => ReleaseReserve,
            _ => value
        };
    }

    public static bool IsValid(string? type)
    {
        var normalized = Normalize(type);
        return All.Contains(normalized);
    }
}

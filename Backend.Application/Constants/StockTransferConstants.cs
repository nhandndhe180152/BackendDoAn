namespace Backend.Application.Constants;

/// <summary>
/// Cách xử lý từng bao khi lấy hàng ở kho nguồn (verify chất lượng trước khi chuyển).
/// </summary>
public static class StockTransferBagDispositions
{
    /// <summary>Bao đạt chất lượng — chuyển sang kho đích.</summary>
    public const string Transfer = "TRANSFER";
    /// <summary>Bao không đạt — chuyển vào khu cách ly ở kho nguồn (tự chọn ô cách ly).</summary>
    public const string Quarantine = "QUARANTINE";
    /// <summary>Bao không đạt — bỏ nguyên bao khỏi kho nguồn.</summary>
    public const string Dispose = "DISPOSE";

    public static readonly string[] All = [Transfer, Quarantine, Dispose];

    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && System.Array.IndexOf(All, value) >= 0;
}

/// <summary>
/// Loại nguồn của phiếu nhập kho (InboundOrder.SourceType).
/// </summary>
public static class InboundOrderSourceTypeConstants
{
    public const string Receipt       = "RECEIPT";
    public const string PurchaseOrder = "PO";
    public const string Manual        = "MANUAL";
    public const string PaddyPurchase = "PADDY_PURCHASE";
    /// <summary>Phiếu nhập tự sinh ở kho đích khi hoàn tất chuyển kho nội bộ.</summary>
    public const string StockTransfer = "STOCK_TRANSFER";
}

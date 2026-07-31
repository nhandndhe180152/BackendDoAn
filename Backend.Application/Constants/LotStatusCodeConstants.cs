namespace Backend.Application.Constants;

public static class LotStatusCodeConstants
{
    /// <summary>Lô vừa sinh sau khi chốt phiếu mua, đang chờ kiểm định chất lượng.
    /// Chỉ hiện ở màn Chất lượng &amp; cách ly, KHÔNG hiện ở màn Nhập kho cho tới khi kiểm định xong.</summary>
    public const string AwaitingQc     = "AWAITING_QC";
    public const string PendingInbound = "PENDING_INBOUND";
    public const string InStock        = "IN_STOCK";
    public const string Processing     = "PROCESSING";
    public const string Quarantine     = "QUARANTINE";
    public const string Milling        = "MILLING";
    public const string Depleted       = "DEPLETED";
}

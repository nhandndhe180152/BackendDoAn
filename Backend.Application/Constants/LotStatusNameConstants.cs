namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái lô (LotStatus.Name) theo seed (LotStatusSeed).
/// Dùng để phân loại tồn "Cách ly" (CL) và "Đang xử lý" (XL) khi tổng hợp giám sát tồn kho.
/// LotStatus không có cột Code nên đối chiếu theo Name seed ổn định.
/// </summary>
public static class LotStatusNameConstants
{
    public const string AwaitingQc     = "Chờ kiểm định";
    public const string PendingInbound = "Chờ nhập";
    public const string InStock        = "Đang lưu kho";
    public const string Processing     = "Chờ xử lý";
    public const string Quarantine     = "Cách ly";
    public const string Milling        = "Đang xay";
    public const string Depleted       = "Đã dùng hết";
    public const string RejectedReturn = "Trả lại sau kiểm định";
}

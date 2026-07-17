namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái đơn bán (SalesOrder) — khớp với SalesOrderStatusSeed.
/// </summary>
public static class SalesOrderStatusNames
{
    public const string New              = "Mới tạo";
    public const string PendingConfirm   = "Chờ xác nhận";
    public const string Reserved         = "Đã giữ hàng";
    public const string AwaitingMilling  = "Chờ xay";
    public const string Preparing        = "Đang chuẩn bị";
    public const string Delivering       = "Đang giao";
    public const string Completed        = "Hoàn tất";
    public const string Cancelled        = "Hủy";
}

namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái đơn bán (SalesOrder) — khớp với SalesOrderStatusSeed.
/// </summary>
public static class SalesOrderStatusNames
{
    public const string New              = "NEW";
    public const string PendingConfirm   = "PENDING_CONFIRM";
    public const string Reserved         = "RESERVED";
    public const string AwaitingMilling  = "AWAITING_MILLING";
    public const string Preparing        = "PREPARING";
    public const string Delivering       = "DELIVERING";
    public const string Completed        = "COMPLETED";
    public const string Cancelled        = "CANCELLED";
}

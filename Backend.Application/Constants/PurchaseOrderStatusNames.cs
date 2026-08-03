namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái đơn mua hàng (PurchaseOrder) — khớp với PurchaseOrderStatusSeed.
/// </summary>
public static class PurchaseOrderStatusNames
{
    public const string Draft             = "DRAFT";
    public const string Confirmed         = "CONFIRMED";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Received          = "RECEIVED";
    public const string Cancelled         = "CANCELLED";
}

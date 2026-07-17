namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái đơn mua hàng (PurchaseOrder) — khớp với PurchaseOrderStatusSeed.
/// </summary>
public static class PurchaseOrderStatusNames
{
    public const string Draft             = "Draft";
    public const string Confirmed         = "Confirmed";
    public const string PartiallyReceived = "PartiallyReceived";
    public const string Received          = "Received";
    public const string Cancelled         = "Cancelled";
}

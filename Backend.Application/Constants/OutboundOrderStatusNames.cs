namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái phiếu xuất (OutboundOrder) — khớp với OutboundOrderStatusSeed.
/// </summary>
public static class OutboundOrderStatusNames
{
    public const string Draft      = "DRAFT";
    public const string Picking    = "PICKING";
    public const string Packed     = "PACKED";
    public const string Dispatched = "DISPATCHED";
    public const string Completed  = "COMPLETED";
    public const string Cancelled  = "CANCELLED";
    public const string DeliveryFailed = "DELIVERY_FAILED";
}

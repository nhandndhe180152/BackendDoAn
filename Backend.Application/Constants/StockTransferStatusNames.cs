namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái phiếu chuyển kho được seed trong StockTransferStatus.
/// </summary>
public static class StockTransferStatusNames
{
    public const string Draft = "DRAFT";
    public const string InTransit = "IN_TRANSIT";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

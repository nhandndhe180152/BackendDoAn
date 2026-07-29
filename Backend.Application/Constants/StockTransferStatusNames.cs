namespace Backend.Application.Constants;

/// <summary>
/// Tên trạng thái phiếu chuyển kho được seed trong StockTransferStatus.
/// </summary>
public static class StockTransferStatusNames
{
    public const string Draft = "Nháp";
    public const string InTransit = "Đang chuyển";
    public const string Completed = "Hoàn tất";
    public const string Cancelled = "Hủy";
}

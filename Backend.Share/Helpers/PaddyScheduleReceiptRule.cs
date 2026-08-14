namespace Backend.Share.Helpers;

/// <summary>
/// Quy tắc dùng chung xác định một lịch thu mua còn được lập thêm phiếu mua lúa hay không.
/// Dùng ở cả tầng service (chặn khi tạo/sửa phiếu) và tầng đọc (trả cờ CanCreateReceipt cho FE),
/// để web, mobile và backend luôn khớp nhau.
/// </summary>
public static class PaddyScheduleReceiptRule
{
    /// <summary>Sai số cho phép khi so sánh khối lượng (kg) — tránh lệch do làm tròn 0,1 kg.</summary>
    public const decimal WeightTolerance = 0.001m;

    /// <summary>Các trạng thái lịch không cho lập thêm phiếu.</summary>
    public static bool IsBlockedByStatus(string? statusCode)
    {
        var code = statusCode?.Trim().ToUpperInvariant();
        return code is "CANCELLED" or "STOCKED" or "PARTIALLY_STOCKED";
    }

    /// <summary>
    /// Lịch đã được lập đủ phiếu chưa.
    /// - Có khối lượng dự kiến: đủ khi tổng khối lượng các phiếu chưa xóa >= khối lượng dự kiến.
    /// - Không khai báo khối lượng dự kiến: coi là đủ ngay khi đã có 1 phiếu (không có mốc để chia nhiều phiếu).
    /// </summary>
    public static bool IsFullyReceipted(decimal? estimatedQtyKg, decimal receiptedWeightKg, int receiptCount)
    {
        if (estimatedQtyKg is > 0)
            return receiptedWeightKg >= estimatedQtyKg.Value - WeightTolerance;

        return receiptCount > 0;
    }

    /// <summary>Lịch còn được lập thêm phiếu mua lúa hay không.</summary>
    public static bool CanCreateReceipt(
        string? statusCode,
        decimal? estimatedQtyKg,
        decimal receiptedWeightKg,
        int receiptCount)
    {
        if (IsBlockedByStatus(statusCode)) return false;
        return !IsFullyReceipted(estimatedQtyKg, receiptedWeightKg, receiptCount);
    }

    /// <summary>Khối lượng còn lại có thể lập phiếu (null khi lịch không khai báo khối lượng dự kiến).</summary>
    public static decimal? RemainingQtyKg(decimal? estimatedQtyKg, decimal receiptedWeightKg)
    {
        if (estimatedQtyKg is not > 0) return null;
        var remaining = estimatedQtyKg.Value - receiptedWeightKg;
        return remaining > 0 ? remaining : 0m;
    }
}

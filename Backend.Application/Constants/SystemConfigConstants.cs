namespace Backend.Application.Constants;

/// <summary>
/// Khóa cấu hình hệ thống (bảng SystemConfig) và giá trị mặc định dùng khi chưa cấu hình.
/// Admin có thể chỉnh trong màn "Cấu hình hệ thống".
/// </summary>
public static class SystemConfigConstants
{
    public static class Keys
    {
        /// <summary>Tên hệ thống hiển thị trong email.</summary>
        public const string SystemName = "SystemName";

        /// <summary>Đường dẫn (URL) logo hệ thống hiển thị trong email.</summary>
        public const string SystemLogoUrl = "SystemLogoUrl";

        // ── Ngưỡng phân loại chênh lệch kiểm kê kho (StockTake) ─────────────────
        // StockTakeService đọc 2 key này để phân loại VarianceSeverity = SMALL / MEDIUM / LARGE.
        // Chênh lệch % ≤ SmallVariancePercent          → SMALL
        // Chênh lệch % ≤ MediumVariancePercent          → MEDIUM
        // Chênh lệch % > MediumVariancePercent          → LARGE (bắt buộc nhập lý do)

        /// <summary>Ngưỡng trên của mức SMALL (% so với tồn hệ thống).</summary>
        public const string StockTakeSmallVariancePercent = "StockTakeSmallVariancePercent";

        /// <summary>Ngưỡng trên của mức MEDIUM (% so với tồn hệ thống).</summary>
        public const string StockTakeMediumVariancePercent = "StockTakeMediumVariancePercent";
    }

    public static class Defaults
    {
        public const string SystemName = "Tuấn Mây";
        public const string SystemLogoUrl = "";

        // Giá trị mặc định khi chưa cấu hình trong DB
        public const decimal StockTakeSmallVariancePercent = 1.0m;
        public const decimal StockTakeMediumVariancePercent = 5.0m;
    }
}

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
        // Mức cuối = MAX(phân loại theo %, phân loại theo kg).
        // SMALL  : variancePercent <= Small%  VÀ absoluteKg <= SmallKg
        // MEDIUM : variancePercent <= Medium% HOẶC absoluteKg <= MediumKg  (và chưa đạt LARGE)
        // LARGE  : variancePercent >  Medium% HOẶC absoluteKg >  MediumKg

        /// <summary>Ngưỡng trên của mức SMALL theo % (FDS: 0.5%).</summary>
        public const string StockTakeSmallVariancePercent = "StockTakeSmallVariancePercent";

        /// <summary>Ngưỡng trên của mức MEDIUM theo % (FDS: 2%).</summary>
        public const string StockTakeMediumVariancePercent = "StockTakeMediumVariancePercent";

        /// <summary>Ngưỡng trên của mức SMALL theo kg (FDS: 5 kg).</summary>
        public const string StockTakeSmallVarianceKg = "StockTakeSmallVarianceKg";

        /// <summary>Ngưỡng trên của mức MEDIUM theo kg (FDS: 20 kg).</summary>
        public const string StockTakeMediumVarianceKg = "StockTakeMediumVarianceKg";
    }

    public static class Defaults
    {
        public const string SystemName = "Tuấn Mây";
        public const string SystemLogoUrl = "";

        // Giá trị mặc định khi chưa cấu hình trong DB (theo FDS)
        public const decimal StockTakeSmallVariancePercent  = 0.5m;
        public const decimal StockTakeMediumVariancePercent = 2.0m;
        public const decimal StockTakeSmallVarianceKg       = 5.0m;
        public const decimal StockTakeMediumVarianceKg      = 20.0m;
    }
}

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
    }

    public static class Defaults
    {
        public const string SystemName = "Tuấn Mây";
        public const string SystemLogoUrl = "";
    }
}

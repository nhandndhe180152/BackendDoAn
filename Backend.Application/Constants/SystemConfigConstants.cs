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

        // Smart Put-away weights keys
        public const string PutawayCategoryMatchWeight = "PutawayCategoryMatchWeight";
        public const string PutawayCapacityFitWeight = "PutawayCapacityFitWeight";
        public const string PutawayOccupancyWeight = "PutawayOccupancyWeight";
        public const string PutawayPriorityWeight = "PutawayPriorityWeight";
        public const string PutawaySameProductScore = "PutawaySameProductScore";
        public const string PutawayEmptyColumnScore = "PutawayEmptyColumnScore";
    }

    public static class Defaults
    {
        public const string SystemName = "Tuấn Mây";
        public const string SystemLogoUrl = "";

        // Smart Put-away weights defaults
        public const string PutawayCategoryMatchWeight = "0.20";
        public const string PutawayCapacityFitWeight = "0.40";
        public const string PutawayOccupancyWeight = "0.30";
        public const string PutawayPriorityWeight = "0.10";
        public const string PutawaySameProductScore = "1.00";
        public const string PutawayEmptyColumnScore = "0.60";
    }
}

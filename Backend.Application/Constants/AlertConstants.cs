namespace Backend.Application.Constants;

/// <summary>
/// Hằng số cho Cảnh báo (Alert) — trạng thái, mức độ và loại cảnh báo (SCR-21).
/// Alert do các background job lúa/gạo sinh ra; màn hình dùng các trạng thái này để duyệt/xử lý.
/// </summary>
public static class AlertConstants
{
    public static class Status
    {
        public const string Open = "OPEN";
        public const string Acknowledged = "ACKNOWLEDGED";
        public const string Resolved = "RESOLVED";
    }

    public static class Severity
    {
        public const string Info = "INFO";
        public const string Warning = "WARNING";
        public const string Critical = "CRITICAL";
    }

    public static class Type
    {
        public const string LowStock = "LOW_STOCK";
        public const string IntakeBottleneck = "INTAKE_BOTTLENECK";
        public const string LotQuality = "LOT_QUALITY";
        public const string DebtOverdue = "DEBT_OVERDUE";
    }
}

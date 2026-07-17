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

    /// <summary>
    /// Mã quy tắc cảnh báo (bật/tắt) hiển thị ở khối "Quy tắc cảnh báo" (SCR-21).
    /// Trạng thái bật/tắt được lưu trong bảng SystemConfig theo <see cref="RuleEnabledKey"/>
    /// để không phải tạo bảng/migration mới (theo đúng khuôn Smart Put-away).
    /// </summary>
    public static class Rules
    {
        public const string LowStock = "LOW_STOCK";
        public const string WarehouseCapacity = "WAREHOUSE_CAPACITY";
        public const string ExpirySoon = "EXPIRY_SOON";
    }

    /// <summary>
    /// Khoá SystemConfig lưu bật/tắt của 1 quy tắc, theo khuôn "base:scope" giống Put-away:
    /// "AlertRuleEnabled:{ruleCode}" (vd "AlertRuleEnabled:LOW_STOCK").
    /// </summary>
    public static string RuleEnabledKey(string ruleCode) =>
        $"{CommonConstants.SystemConfig.ALERT_RULE_ENABLED_KEY}:{ruleCode}";
}

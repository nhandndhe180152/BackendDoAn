using System;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public class IntakeBottleneckCalculationInput
{
    public decimal ExpectedIntakeKg { get; set; }
    public decimal FreeStorageCapacityKg { get; set; }
    public decimal IntakeLabourCapacityKg { get; set; }
    public decimal WarningRatio { get; set; }
    public decimal CriticalRatio { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
}

public class IntakeBottleneckCalculationResult
{
    public decimal ExpectedIntakeKg { get; set; }
    public decimal FreeStorageCapacityKg { get; set; }
    public decimal IntakeLabourCapacityKg { get; set; }
    public decimal EffectiveCapacityKg { get; set; }
    public decimal StorageLoadRatio { get; set; }
    public decimal LabourLoadRatio { get; set; }
    public decimal BottleneckRatio { get; set; }
    public string LimitingResource { get; set; } = null!;
    public string Classification { get; set; } = null!;
    public decimal WarningRatio { get; set; }
    public decimal CriticalRatio { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public bool HasConfigError { get; set; }
    public string? ConfigErrorDetail { get; set; }
}

public static class IntakeBottleneckConstants
{
    public static class LimitingResource
    {
        public const string Storage = "STORAGE";
        public const string Labour = "LABOUR";
        public const string Both = "BOTH";
        public const string None = "NONE";
    }

    public static class Classification
    {
        public const string Normal = "NORMAL";
        public const string Warning = "WARNING";
        public const string Critical = "CRITICAL";
    }

    public static class Action
    {
        public const string Created = "CREATED";
        public const string Updated = "UPDATED";
        public const string Escalated = "ESCALATED";
        public const string Downgraded = "DOWNGRADED";
        public const string Resolved = "RESOLVED";
        public const string ConfigWarning = "CONFIG_WARNING";
        public const string Unchanged = "UNCHANGED";
        public const string Skipped = "SKIPPED";
        public const string Failed = "FAILED";
    }

    public static class ConfigKey
    {
        public const string WindowHours = "IntakeBottleneckWindowHours";
        public const string WarningRatio = "IntakeBottleneckWarningRatio";
        public const string CriticalRatio = "IntakeBottleneckCriticalRatio";
        public const string LabourCapacityPrefix = "IntakeLabourCapacity:";
        // Key toàn cục (không kèm WarehouseId) - dùng làm fallback chung cho mọi kho.
        public const string LabourCapacityGlobal = "IntakeLabourCapacity";
    }

    public static class Default
    {
        // Năng lực nhân công tiếp nhận mặc định (kg) khi không có cấu hình theo kho lẫn toàn cục.
        public const decimal LabourCapacityKg = 50000m;
    }

    public static class ScheduleStatus
    {
        public const string Confirmed = "CONFIRMED";
        public const string Collecting = "COLLECTING";
        public const string Stocked = "STOCKED";
    }

    public static class Job
    {
        public const string Id = "job-02-intake-bottleneck-evaluation";
        public const string LogPrefix = "[JOB-02]";
        public const string LockKeyPrefix = "stocklite:job-02";
        public const string DeduplicationKeyPrefix = "INTAKE_BOTTLENECK";
        public const string ConfigAlertType = "INTAKE_BOTTLENECK_CONFIGURATION";
    }
}

public class IntakeBottleneckJobResult
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int AlertsCreated { get; set; }
    public int AlertsUpdated { get; set; }
    public int AlertsResolved { get; set; }
    public int NotificationsQueued { get; set; }
    public long DurationMs { get; set; }
    public bool SkippedByLock { get; set; }
}

public class IntakeBottleneckWarehouseResult
{
    public int WarehouseId { get; set; }
    public string Action { get; set; } = null!; // CREATED, UPDATED, UNCHANGED, ESCALATED, DOWNGRADED, RESOLVED, SKIPPED, CONFIG_WARNING
    public IntakeBottleneckCalculationResult? CalculationResult { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ConfigAlertCreated { get; set; }
    public bool ConfigAlertUpdated { get; set; }
    public bool ConfigAlertResolved { get; set; }
}

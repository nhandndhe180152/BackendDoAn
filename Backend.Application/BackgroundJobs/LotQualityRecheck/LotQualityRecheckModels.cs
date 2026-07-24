using System;
using System.Collections.Generic;

namespace Backend.Application.BackgroundJobs.LotQualityRecheck;

public class LotQualityRecheckJobResult
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

public class LotQualityRecheckLotEvaluationResult
{
    public int LotId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Actions { get; set; } = new();
}

public static class LotQualityRecheckConstants
{
    public static class AlertType
    {
        public const string InspectionDue = "INSPECTION_DUE";
        public const string HighMoisture = "HIGH_MOISTURE";
        public const string MouldRisk = "MOULD_RISK";
        public const string LongStoredLot = "LONG_STORED_LOT";
        public const string LotQualityRecheckConfigInvalid = "JOB_03_CONFIG_INVALID";
    }

    public static class ConfigKey
    {
        public const string IntervalDays = "QualityInspectionIntervalDays";
        public const string OverdueCriticalDays = "InspectionOverdueCriticalDays";
        public const string MoistureWarning = "MoistureWarningThreshold";
        public const string MoistureCritical = "MoistureCriticalThreshold";
        public const string MoldWarning = "MoldWarningLevel";
        public const string MoldCritical = "MoldCriticalLevel";
        public const string LongStoredWarning = "LongStoredWarningDays";
        public const string LongStoredCritical = "LongStoredCriticalDays";
    }

    public static class Action
    {
        public const string Created = "CREATED";
        public const string Updated = "UPDATED";
        public const string Escalated = "ESCALATED";
        public const string Resolved = "RESOLVED";
        public const string ConfigWarning = "CONFIG_WARNING";
        public const string Unchanged = "UNCHANGED";
        public const string Skipped = "SKIPPED";
        public const string Failed = "FAILED";
    }

    public static class Job
    {
        public const string LockKeyPrefix = "stocklite:job-03";
        public const string DeduplicationKeyPrefix = "LotQualityRecheck";
        public const string Id = "job-03-lot-quality-recheck";
        public const string LogPrefix = "[JOB-03]";
    }
}

public static class LotQualityRecheckHelper
{
    public static int ParseMoldLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return 0;
        var normalized = level.Trim().ToUpperInvariant();
        return normalized switch
        {
            "KHONG" or "NONE" or "KHÔNG" => 0,
            "NHE" or "LIGHT" or "NHẸ" => 1,
            "NANG" or "HEAVY" or "NẶNG" => 2,
            _ => 0
        };
    }
}

using System;
using System.Collections.Generic;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public class DebtDueOverdueJobResult
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int PayableProcessed { get; set; }
    public int ReceivableProcessed { get; set; }
    public int DueSoonCreated { get; set; }
    public int OverdueCreated { get; set; }
    public int AlertsUpdated { get; set; }
    public int AlertsEscalated { get; set; }
    public int AlertsResolved { get; set; }
    public int NotificationsQueued { get; set; }
    public long DurationMs { get; set; }
    public bool SkippedByLock { get; set; }
}

public class DebtDueOverduePartyEvaluationResult
{
    public int PartyDebtId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Actions { get; set; } = new();
}

public class DebtDueOverdueConfig
{
    public int PartyDebtId { get; set; }
    public string Direction { get; set; } = null!;
    public int ReminderLeadDays { get; set; }
    public string DueTodaySeverity { get; set; } = null!;
    public int OverdueWarningDays { get; set; }
    public int OverdueCriticalDays { get; set; }
    public decimal OverdueWarningAmount { get; set; }
    public decimal OverdueCriticalAmount { get; set; }
}

public class DebtAgingCalculationResult
{
    public int PartyDebtId { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal DueSoonAmount { get; set; }
    public decimal DueTodayAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal NotYetDueAmount { get; set; }
    public decimal UnscheduledOutstandingAmount { get; set; }
    public DateTime? NearestDueDate { get; set; }
    public DateTime? OldestOverdueDate { get; set; }
    public int MaxDaysOverdue { get; set; }
    public int OpenChargeCount { get; set; }
    public decimal ReconciliationDifference { get; set; }
}

public class DebtDueOverdueRuleEvaluation
{
    public string AlertType { get; set; } = null!;
    public bool ShouldAlert { get; set; }
    public string Severity { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string DeduplicationKey { get; set; } = null!;
    public string ConditionFingerprint { get; set; } = null!;
}

public static class DebtDueOverdueConstants
{
    public static class AlertType
    {
        public const string DebtDueSoon = "DEBT_DUE_SOON";
        public const string DebtOverdue = "DEBT_OVERDUE";
        public const string DebtConfigInvalid = "DEBT_CONFIG_INVALID";
        public const string DebtDataAnomaly = "DEBT_DATA_ANOMALY";
    }

    public static class ConfigKey
    {
        public const string LeadDays = "DebtReminderLeadDays";
        public const string DueTodaySeverity = "DebtDueTodaySeverity";
        public const string OverdueWarningDays = "DebtOverdueWarningDays";
        public const string OverdueCriticalDays = "DebtOverdueCriticalDays";
        public const string OverdueWarningAmount = "DebtOverdueWarningAmount";
        public const string OverdueCriticalAmount = "DebtOverdueCriticalAmount";
    }

    public static class Action
    {
        public const string Created = "CREATED";
        public const string Updated = "UPDATED";
        public const string Escalated = "ESCALATED";
        public const string Resolved = "RESOLVED";
        public const string Unchanged = "UNCHANGED";
    }

    public static class Job
    {
        public const string LockKeyPrefix = "stocklite:job-04";
        public const string DeduplicationKeyPrefix = "JOB04";
        public const string Id = "job-04-debt-due-reminder";
        public const string LogPrefix = "[JOB-04]";
    }
}

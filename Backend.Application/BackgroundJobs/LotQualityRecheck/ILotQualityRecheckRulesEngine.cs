using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Application.BackgroundJobs.LotQualityRecheck;

public class LotQualityRecheckWarehouseConfig
{
    public int WarehouseId { get; set; }
    public int InspectionIntervalDays { get; set; }
    public int InspectionOverdueCriticalDays { get; set; }
    public decimal MoistureWarningThreshold { get; set; }
    public decimal MoistureCriticalThreshold { get; set; }
    public int MoldWarningLevel { get; set; } // standard ordinal (0, 1, 2)
    public int MoldCriticalLevel { get; set; } // standard ordinal (0, 1, 2)
    public int LongStoredWarningDays { get; set; }
    public int LongStoredCriticalDays { get; set; }
}

public class LotQualityRecheckRuleEvaluation
{
    public string AlertType { get; set; } = null!;
    public bool ShouldAlert { get; set; }
    public string Severity { get; set; } = null!; // WARNING, CRITICAL, INFO, etc.
    public string Message { get; set; } = null!;
    public string DeduplicationKey { get; set; } = null!;
}

public interface ILotQualityRecheckRulesEngine
{
    List<LotQualityRecheckRuleEvaluation> Evaluate(
        PaddyLot lot,
        QualityInspection? latestInspection,
        LotQualityRecheckWarehouseConfig config,
        List<string> configErrors,
        DateTime businessToday);
}

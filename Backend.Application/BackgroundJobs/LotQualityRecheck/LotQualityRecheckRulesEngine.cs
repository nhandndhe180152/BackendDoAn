using System;
using System.Collections.Generic;
using Backend.Domain.Entities;
using Backend.Application.Constants;

namespace Backend.Application.BackgroundJobs.LotQualityRecheck;

public class LotQualityRecheckRulesEngine : ILotQualityRecheckRulesEngine
{
    public List<LotQualityRecheckRuleEvaluation> Evaluate(
        PaddyLot lot,
        QualityInspection? latestInspection,
        LotQualityRecheckWarehouseConfig config,
        List<string> configErrors,
        DateTime businessToday)
    {
        var evaluations = new List<LotQualityRecheckRuleEvaluation>();

        bool isQuarantined = (lot.Status != null && lot.Status.Code == LotStatusCodeConstants.Quarantine) ||
                             (lot.Location != null && lot.Location.IsQuarantine);

        // 1. Rule: Inspection Due
        if (!configErrors.Contains(LotQualityRecheckConstants.ConfigKey.IntervalDays) &&
            !configErrors.Contains(LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays))
        {
            evaluations.Add(EvaluateInspectionDue(lot, latestInspection, config, businessToday));
        }

        // 2. Rule: Long Stored
        if (!configErrors.Contains(LotQualityRecheckConstants.ConfigKey.LongStoredWarning) &&
            !configErrors.Contains(LotQualityRecheckConstants.ConfigKey.LongStoredCritical))
        {
            evaluations.Add(EvaluateLongStored(lot, config, businessToday, isQuarantined));
        }

        // 3. Rule: High Moisture
        if (!configErrors.Contains(LotQualityRecheckConstants.ConfigKey.MoistureWarning) &&
            !configErrors.Contains(LotQualityRecheckConstants.ConfigKey.MoistureCritical))
        {
            evaluations.Add(EvaluateHighMoisture(lot, latestInspection, config, isQuarantined));
        }

        // 4. Rule: Mould Risk
        if (!configErrors.Contains(LotQualityRecheckConstants.ConfigKey.MoldWarning) &&
            !configErrors.Contains(LotQualityRecheckConstants.ConfigKey.MoldCritical))
        {
            evaluations.Add(EvaluateMouldRisk(lot, latestInspection, config, isQuarantined));
        }

        return evaluations;
    }

    private LotQualityRecheckRuleEvaluation EvaluateInspectionDue(
        PaddyLot lot,
        QualityInspection? latestInspection,
        LotQualityRecheckWarehouseConfig config,
        DateTime businessToday)
    {
        var alertType = LotQualityRecheckConstants.AlertType.InspectionDue;
        var dedupKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}:PADDY_LOT:{lot.Id}:{alertType}";

        bool shouldAlert;
        string severity = AlertConstants.Severity.Warning;
        string message;
        int overdueDays = 0;

        if (latestInspection == null)
        {
            var dueAt = lot.InboundDate.Date.AddDays(config.InspectionIntervalDays);
            shouldAlert = businessToday.Date >= dueAt;
            overdueDays = Math.Max(0, (businessToday.Date - dueAt).Days);

            if (overdueDays >= config.InspectionOverdueCriticalDays)
            {
                severity = AlertConstants.Severity.Critical;
            }

            message = $"Lô {lot.LotCode} chưa từng được kiểm định (nhập kho từ ngày {lot.InboundDate:dd/MM/yyyy}). Vui lòng thực hiện kiểm định chất lượng.";
        }
        else
        {
            var dueAt = latestInspection.InspectedAt.Date.AddDays(config.InspectionIntervalDays);
            shouldAlert = businessToday.Date >= dueAt;
            overdueDays = Math.Max(0, (businessToday.Date - dueAt).Days);

            if (shouldAlert)
            {
                if (overdueDays >= config.InspectionOverdueCriticalDays)
                {
                    severity = AlertConstants.Severity.Critical;
                }

                message = $"Lô {lot.LotCode} đã đến hạn kiểm định lại từ ngày {dueAt:dd/MM/yyyy}. Vui lòng thực hiện kiểm định chất lượng.";
            }
            else
            {
                message = $"Lô {lot.LotCode} đã kiểm định và còn hạn đến ngày {dueAt:dd/MM/yyyy}.";
            }
        }

        return new LotQualityRecheckRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey
        };
    }

    private LotQualityRecheckRuleEvaluation EvaluateHighMoisture(
        PaddyLot lot,
        QualityInspection? latestInspection,
        LotQualityRecheckWarehouseConfig config,
        bool isQuarantined)
    {
        var alertType = LotQualityRecheckConstants.AlertType.HighMoisture;
        var dedupKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}:PADDY_LOT:{lot.Id}:{alertType}";

        if (latestInspection == null || !latestInspection.MoisturePercent.HasValue)
        {
            return new LotQualityRecheckRuleEvaluation
            {
                AlertType = alertType,
                ShouldAlert = false,
                Severity = AlertConstants.Severity.Warning,
                Message = $"Lô {lot.LotCode} chưa có thông tin kiểm định độ ẩm.",
                DeduplicationKey = dedupKey
            };
        }

        var moisture = latestInspection.MoisturePercent.Value;
        bool shouldAlert = moisture > config.MoistureWarningThreshold;

        if (isQuarantined)
        {
            shouldAlert = false;
        }

        string severity = AlertConstants.Severity.Warning;
        if (moisture > config.MoistureCriticalThreshold)
        {
            severity = AlertConstants.Severity.Critical;
        }

        string message = severity == AlertConstants.Severity.Critical
            ? $"Lô {lot.LotCode} có độ ẩm {moisture:0.##}% vượt ngưỡng nghiêm trọng {config.MoistureCriticalThreshold:0.##}%. Cần kiểm tra và cân nhắc sấy, đảo lô hoặc cách ly."
            : $"Lô {lot.LotCode} có độ ẩm {moisture:0.##}% vượt ngưỡng cảnh báo {config.MoistureWarningThreshold:0.##}%. Cần kiểm tra và cân nhắc sấy, đảo lô hoặc cách ly.";

        return new LotQualityRecheckRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey
        };
    }

    private LotQualityRecheckRuleEvaluation EvaluateMouldRisk(
        PaddyLot lot,
        QualityInspection? latestInspection,
        LotQualityRecheckWarehouseConfig config,
        bool isQuarantined)
    {
        var alertType = LotQualityRecheckConstants.AlertType.MouldRisk;
        var dedupKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}:PADDY_LOT:{lot.Id}:{alertType}";

        if (latestInspection == null || string.IsNullOrWhiteSpace(latestInspection.MoldLevel))
        {
            return new LotQualityRecheckRuleEvaluation
            {
                AlertType = alertType,
                ShouldAlert = false,
                Severity = AlertConstants.Severity.Warning,
                Message = $"Lô {lot.LotCode} chưa có thông tin kiểm định mức mốc.",
                DeduplicationKey = dedupKey
            };
        }

        var actualLevel = LotQualityRecheckHelper.ParseMoldLevel(latestInspection.MoldLevel);
        bool shouldAlert = actualLevel >= config.MoldWarningLevel;

        if (isQuarantined)
        {
            shouldAlert = false;
        }

        string severity = AlertConstants.Severity.Warning;
        if (actualLevel >= config.MoldCriticalLevel)
        {
            severity = AlertConstants.Severity.Critical;
        }

        string message = $"Lô {lot.LotCode} có mức mốc {latestInspection.MoldLevel} vượt ngưỡng cho phép. Cần kiểm tra và cân nhắc đưa lô vào khu vực cách ly.";

        return new LotQualityRecheckRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey
        };
    }

    private LotQualityRecheckRuleEvaluation EvaluateLongStored(
        PaddyLot lot,
        LotQualityRecheckWarehouseConfig config,
        DateTime businessToday,
        bool isQuarantined)
    {
        var alertType = LotQualityRecheckConstants.AlertType.LongStoredLot;
        var dedupKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}:PADDY_LOT:{lot.Id}:{alertType}";

        var storedAgeDays = (businessToday.Date - lot.InboundDate.Date).Days;
        bool shouldAlert = storedAgeDays >= config.LongStoredWarningDays;

        string severity = AlertConstants.Severity.Warning;
        if (storedAgeDays >= config.LongStoredCriticalDays)
        {
            shouldAlert = true;
            severity = AlertConstants.Severity.Critical;
        }

        string message;
        var thresholdString = severity == AlertConstants.Severity.Critical ? config.LongStoredCriticalDays : config.LongStoredWarningDays;
        if (isQuarantined)
        {
            message = $"[ĐÃ CÁCH LY] Lô {lot.LotCode} đã lưu kho {storedAgeDays} ngày, vượt ngưỡng {thresholdString} ngày. Vui lòng kiểm định và lên phương án xử lý.";
        }
        else
        {
            message = $"Lô {lot.LotCode} đã lưu kho {storedAgeDays} ngày, vượt ngưỡng {thresholdString} ngày. Vui lòng kiểm định và lên phương án xử lý.";
        }

        return new LotQualityRecheckRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey
        };
    }

}

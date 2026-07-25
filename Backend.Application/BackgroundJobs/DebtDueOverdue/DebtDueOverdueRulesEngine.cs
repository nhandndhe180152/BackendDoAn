using System;
using System.Collections.Generic;
using Backend.Application.Constants;
using Backend.Domain.Entities;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public class DebtDueOverdueRulesEngine : IDebtDueOverdueRulesEngine
{
    public List<DebtDueOverdueRuleEvaluation> Evaluate(
        PartyDebt partyDebt,
        string partyName,
        DebtAgingCalculationResult agingResult,
        DebtDueOverdueConfig config,
        List<string> configErrors,
        DateTime businessToday)
    {
        var evaluations = new List<DebtDueOverdueRuleEvaluation>();

        // 1. Rule: Debt Due Soon / Due Today
        if (!configErrors.Contains(DebtDueOverdueConstants.ConfigKey.LeadDays) &&
            !configErrors.Contains(DebtDueOverdueConstants.ConfigKey.DueTodaySeverity))
        {
            evaluations.Add(EvaluateDueSoon(partyDebt, partyName, agingResult, config, businessToday));
        }

        // 2. Rule: Debt Overdue
        if (!configErrors.Contains(DebtDueOverdueConstants.ConfigKey.OverdueWarningDays) &&
            !configErrors.Contains(DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays) &&
            !configErrors.Contains(DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount) &&
            !configErrors.Contains(DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount))
        {
            evaluations.Add(EvaluateOverdue(partyDebt, partyName, agingResult, config, businessToday));
        }

        return evaluations;
    }

    private DebtDueOverdueRuleEvaluation EvaluateDueSoon(
        PartyDebt partyDebt,
        string partyName,
        DebtAgingCalculationResult agingResult,
        DebtDueOverdueConfig config,
        DateTime businessToday)
    {
        var alertType = DebtDueOverdueConstants.AlertType.DebtDueSoon;
        var orgPrefix = partyDebt.OrganizationId.HasValue ? $"JOB04:{partyDebt.OrganizationId.Value}" : "JOB04";
        var dedupKey = $"{orgPrefix}:{AlertConstants.RelatedEntityType.PartyDebt}:{partyDebt.Id}:{alertType}";

        decimal amount = agingResult.DueSoonAmount + agingResult.DueTodayAmount;
        bool shouldAlert = amount > 0m;

        string severity = AlertConstants.Severity.Info;
        if (agingResult.DueTodayAmount > 0m)
        {
            severity = config.DueTodaySeverity;
        }

        string message = "";
        string dueDateStr = agingResult.NearestDueDate?.ToString("dd/MM/yyyy") ?? "";
        string amountStr = amount.ToString("N0") + " VNĐ";

        if (partyDebt.Direction == LookupCodes.DebtDirection.Payable)
        {
            message = $"Khoản phải trả cho {partyName} với số tiền {amountStr} sẽ đến hạn vào ngày {dueDateStr}. Vui lòng kiểm tra và chuẩn bị thanh toán.";
        }
        else
        {
            message = $"Khoản phải thu của {partyName} với số tiền {amountStr} sẽ đến hạn vào ngày {dueDateStr}. Vui lòng kiểm tra và liên hệ theo quy trình thu nợ.";
        }

        // Fingerprint elements: open charge count/IDs, nearest due date, severity, config lead days
        var fingerprint = $"DUE_SOON:{partyDebt.Id}:{agingResult.OpenChargeCount}:{dueDateStr}:{severity}:{config.ReminderLeadDays}";

        return new DebtDueOverdueRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey,
            ConditionFingerprint = fingerprint
        };
    }

    private DebtDueOverdueRuleEvaluation EvaluateOverdue(
        PartyDebt partyDebt,
        string partyName,
        DebtAgingCalculationResult agingResult,
        DebtDueOverdueConfig config,
        DateTime businessToday)
    {
        var alertType = DebtDueOverdueConstants.AlertType.DebtOverdue;
        var orgPrefix = partyDebt.OrganizationId.HasValue ? $"JOB04:{partyDebt.OrganizationId.Value}" : "JOB04";
        var dedupKey = $"{orgPrefix}:{AlertConstants.RelatedEntityType.PartyDebt}:{partyDebt.Id}:{alertType}";

        decimal amount = agingResult.OverdueAmount;
        bool shouldAlert = amount > 0m;

        string daysSeverity = AlertConstants.Severity.Info;
        if (agingResult.MaxDaysOverdue >= config.OverdueCriticalDays)
        {
            daysSeverity = AlertConstants.Severity.Critical;
        }
        else if (agingResult.MaxDaysOverdue >= config.OverdueWarningDays)
        {
            daysSeverity = AlertConstants.Severity.Warning;
        }

        string amountSeverity = AlertConstants.Severity.Info;
        if (amount >= config.OverdueCriticalAmount)
        {
            amountSeverity = AlertConstants.Severity.Critical;
        }
        else if (amount >= config.OverdueWarningAmount)
        {
            amountSeverity = AlertConstants.Severity.Warning;
        }

        string severity = MaxSeverity(daysSeverity, amountSeverity);

        string message = "";
        string oldestDueDateStr = agingResult.OldestOverdueDate?.ToString("dd/MM/yyyy") ?? "";
        string amountStr = amount.ToString("N0") + " VNĐ";

        if (partyDebt.Direction == LookupCodes.DebtDirection.Payable)
        {
            message = $"Khoản phải trả cho {partyName} đã quá hạn {agingResult.MaxDaysOverdue} ngày, số tiền còn quá hạn {amountStr}. Vui lòng kiểm tra chứng từ và xử lý thanh toán.";
        }
        else
        {
            message = $"Khoản phải thu của {partyName} đã quá hạn {agingResult.MaxDaysOverdue} ngày, số tiền còn quá hạn {amountStr}. Vui lòng kiểm tra và xử lý thu nợ.";
        }

        // Fingerprint elements: open charge count/IDs, oldest due date, severity, config thresholds
        var fingerprint = $"OVERDUE:{partyDebt.Id}:{agingResult.OpenChargeCount}:{oldestDueDateStr}:{severity}:{config.OverdueWarningDays}:{config.OverdueCriticalDays}:{config.OverdueWarningAmount}:{config.OverdueCriticalAmount}";

        return new DebtDueOverdueRuleEvaluation
        {
            AlertType = alertType,
            ShouldAlert = shouldAlert,
            Severity = severity,
            Message = message,
            DeduplicationKey = dedupKey,
            ConditionFingerprint = fingerprint
        };
    }

    private string MaxSeverity(string s1, string s2)
    {
        int p1 = GetSeverityPriority(s1);
        int p2 = GetSeverityPriority(s2);
        return p1 >= p2 ? s1 : s2;
    }

    private int GetSeverityPriority(string severity)
    {
        return severity switch
        {
            AlertConstants.Severity.Critical => 3,
            AlertConstants.Severity.Warning => 2,
            AlertConstants.Severity.Info => 1,
            _ => 0
        };
    }
}

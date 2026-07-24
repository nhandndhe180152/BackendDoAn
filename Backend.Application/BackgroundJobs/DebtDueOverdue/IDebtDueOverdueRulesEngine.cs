using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public interface IDebtDueOverdueRulesEngine
{
    List<DebtDueOverdueRuleEvaluation> Evaluate(
        PartyDebt partyDebt,
        string partyName,
        DebtAgingCalculationResult agingResult,
        DebtDueOverdueConfig config,
        List<string> configErrors,
        DateTime businessToday);
}

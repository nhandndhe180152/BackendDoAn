using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backend.Domain.Entities;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public interface IDebtAgingCalculationService
{
    DebtAgingCalculationResult CalculatePartyDebtAging(
        PartyDebt partyDebt,
        List<DebtTransaction> transactions,
        DateTime businessToday,
        int reminderLeadDays);

    Task<DebtAgingCalculationResult> CalculatePartyDebtAgingAsync(
        int partyDebtId,
        DateTime businessToday,
        CancellationToken cancellationToken);

    Task<Dictionary<int, DebtAgingCalculationResult>> CalculatePartyDebtsAgingBatchAsync(
        List<int> partyDebtIds,
        DateTime businessToday,
        CancellationToken cancellationToken);
}

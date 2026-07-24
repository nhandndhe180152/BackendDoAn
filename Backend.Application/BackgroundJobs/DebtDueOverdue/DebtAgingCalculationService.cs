using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Backend.Application.Constants;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public class DebtAgingCalculationService : IDebtAgingCalculationService
{
    private readonly IApplicationDbContext _context;
    private readonly IDebtTransactionEffectResolver _effectResolver;

    public DebtAgingCalculationService(
        IApplicationDbContext context,
        IDebtTransactionEffectResolver effectResolver)
    {
        _context = context;
        _effectResolver = effectResolver;
    }

    public DebtAgingCalculationResult CalculatePartyDebtAging(
        PartyDebt partyDebt,
        List<DebtTransaction> transactions,
        DateTime businessToday,
        int reminderLeadDays)
    {
        var activeTx = transactions
            .Where(t => t.PartyDebtId == partyDebt.Id && !t.IsDeleted)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToList();

        // 1. Separate charges (positive effect) and settlements (negative effect)
        var charges = new List<DebtTransaction>();
        decimal totalSettlements = 0m;

        // If OpeningBalance > 0, treat it as an implicit charge at the very beginning
        if (partyDebt.OpeningBalance > 0)
        {
            charges.Add(new DebtTransaction
            {
                Id = 0,
                PartyDebtId = partyDebt.Id,
                TransactionType = LookupCodes.DebtTransactionType.Charge,
                Amount = partyDebt.OpeningBalance,
                TransactionDate = DateTime.MinValue,
                DueDate = null,
                Note = "Opening Balance"
            });
        }
        else if (partyDebt.OpeningBalance < 0)
        {
            // Opening balance is negative, which means customer pre-paid or supplier was pre-paid
            totalSettlements += Math.Abs(partyDebt.OpeningBalance);
        }

        foreach (var tx in activeTx)
        {
            var effect = _effectResolver.GetBalanceEffect(tx.TransactionType, tx.Amount);
            if (effect > 0)
            {
                charges.Add(tx);
            }
            else if (effect < 0)
            {
                totalSettlements += tx.Amount;
            }
        }

        // 2. Perform reconciliation
        decimal totalCharges = charges.Sum(c => c.Amount);
        decimal computedBalance = totalCharges - totalSettlements;
        decimal reconciliationDiff = partyDebt.CurrentBalance - computedBalance;

        // 3. FIFO Allocation using adjusted settlement to match CurrentBalance
        decimal adjustedSettlements = totalCharges - partyDebt.CurrentBalance;
        if (adjustedSettlements < 0m)
        {
            adjustedSettlements = 0m;
        }

        // Order charges: DueDate null last, then DueDate ASC, then TransactionDate ASC, then Id ASC
        var orderedCharges = charges
            .OrderBy(c => c.DueDate == null)
            .ThenBy(c => c.DueDate)
            .ThenBy(c => c.TransactionDate)
            .ThenBy(c => c.Id)
            .ToList();

        decimal remainingSettlement = adjustedSettlements;
        var outstandingCharges = new List<(DebtTransaction Charge, decimal Outstanding)>();

        foreach (var charge in orderedCharges)
        {
            decimal applied = Math.Min(charge.Amount, remainingSettlement);
            remainingSettlement -= applied;
            decimal outstanding = charge.Amount - applied;

            if (outstanding > 0m)
            {
                outstandingCharges.Add((charge, outstanding));
            }
        }

        // 4. Classify outstanding amounts
        var result = new DebtAgingCalculationResult
        {
            PartyDebtId = partyDebt.Id,
            CurrentBalance = partyDebt.CurrentBalance,
            ReconciliationDifference = reconciliationDiff
        };

        foreach (var item in outstandingCharges)
        {
            var charge = item.Charge;
            var outstanding = item.Outstanding;

            result.OpenChargeCount++;

            if (charge.DueDate == null)
            {
                result.UnscheduledOutstandingAmount += outstanding;
            }
            else
            {
                var dueDate = charge.DueDate.Value.Date;
                var daysOverdue = (businessToday.Date - dueDate).Days;
                var daysUntilDue = (dueDate - businessToday.Date).Days;

                if (dueDate < businessToday.Date)
                {
                    result.OverdueAmount += outstanding;
                    if (result.OldestOverdueDate == null || dueDate < result.OldestOverdueDate.Value)
                    {
                        result.OldestOverdueDate = dueDate;
                    }
                    result.MaxDaysOverdue = Math.Max(result.MaxDaysOverdue, daysOverdue);
                }
                else if (dueDate == businessToday.Date)
                {
                    result.DueTodayAmount += outstanding;
                }
                else if (daysUntilDue <= reminderLeadDays)
                {
                    result.DueSoonAmount += outstanding;
                }
                else
                {
                    result.NotYetDueAmount += outstanding;
                }

                if (result.NearestDueDate == null || dueDate < result.NearestDueDate.Value)
                {
                    result.NearestDueDate = dueDate;
                }
            }
        }

        return result;
    }

    public async Task<DebtAgingCalculationResult> CalculatePartyDebtAgingAsync(
        int partyDebtId,
        DateTime businessToday,
        CancellationToken cancellationToken)
    {
        var partyDebt = await _context.PartyDebts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == partyDebtId && !d.IsDeleted, cancellationToken);

        if (partyDebt == null)
        {
            throw new InvalidOperationException($"Sổ công nợ ID {partyDebtId} không tồn tại.");
        }

        var transactions = await _context.DebtTransactions
            .AsNoTracking()
            .Where(t => t.PartyDebtId == partyDebtId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        int leadDays = await GetLeadDaysAsync(partyDebtId, partyDebt.Direction, cancellationToken);

        return CalculatePartyDebtAging(partyDebt, transactions, businessToday, leadDays);
    }

    public async Task<Dictionary<int, DebtAgingCalculationResult>> CalculatePartyDebtsAgingBatchAsync(
        List<int> partyDebtIds,
        DateTime businessToday,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, DebtAgingCalculationResult>();
        if (partyDebtIds == null || partyDebtIds.Count == 0) return results;

        var debts = await _context.PartyDebts
            .AsNoTracking()
            .Where(d => partyDebtIds.Contains(d.Id) && !d.IsDeleted)
            .ToListAsync(cancellationToken);

        var transactions = await _context.DebtTransactions
            .AsNoTracking()
            .Where(t => partyDebtIds.Contains(t.PartyDebtId) && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        var transactionsMap = transactions
            .GroupBy(t => t.PartyDebtId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get lead days overrides per direction
        var configKeys = new List<string>
        {
            DebtDueOverdueConstants.ConfigKey.LeadDays,
            $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:PAYABLE",
            $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:RECEIVABLE"
        };
        // Add specific partyDebtId overrides
        foreach (var id in partyDebtIds)
        {
            configKeys.Add($"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{id}");
        }

        var configs = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => configKeys.Contains(c.ConfigKey) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

        foreach (var debt in debts)
        {
            transactionsMap.TryGetValue(debt.Id, out var txList);
            txList ??= new List<DebtTransaction>();

            int leadDays = TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{debt.Id}",
                TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{debt.Direction}",
                    TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.LeadDays, 7)));

            var result = CalculatePartyDebtAging(debt, txList, businessToday, leadDays);
            results[debt.Id] = result;
        }

        return results;
    }

    private async Task<int> GetLeadDaysAsync(int partyDebtId, string direction, CancellationToken cancellationToken)
    {
        var keys = new List<string>
        {
            DebtDueOverdueConstants.ConfigKey.LeadDays,
            $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{direction}",
            $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{partyDebtId}"
        };

        var configs = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => keys.Contains(c.ConfigKey) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

        return TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{partyDebtId}",
            TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{direction}",
                TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.LeadDays, 7)));
    }

    private int TryGetInt(Dictionary<string, string> dict, string key, int defaultValue)
    {
        if (dict.TryGetValue(key, out var val) && int.TryParse(val, out var res))
        {
            return res;
        }
        return defaultValue;
    }
}

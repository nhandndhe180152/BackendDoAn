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

        decimal totalCharges = Math.Max(0m, partyDebt.OpeningBalance);
        decimal totalSettlements = Math.Max(0m, -partyDebt.OpeningBalance);
        foreach (var tx in activeTx)
        {
            var effect = _effectResolver.GetBalanceEffect(tx.TransactionType, tx.Amount);
            if (effect > 0) totalCharges += effect;
            if (effect < 0) totalSettlements += Math.Abs(effect);
        }

        var documents = CalculateDebtDocuments(partyDebt, activeTx);
        decimal computedBalance = totalCharges - totalSettlements;
        decimal reconciliationDiff = partyDebt.CurrentBalance - computedBalance;
        var result = new DebtAgingCalculationResult
        {
            PartyDebtId = partyDebt.Id,
            CurrentBalance = partyDebt.CurrentBalance,
            ReconciliationDifference = reconciliationDiff
        };

        foreach (var document in documents.Where(x => x.OutstandingAmount > 0m))
        {
            result.OpenChargeCount++;

            if (document.DueDate == null)
            {
                result.UnscheduledOutstandingAmount += document.OutstandingAmount;
            }
            else
            {
                var dueDate = document.DueDate.Value.Date;
                var daysOverdue = (businessToday.Date - dueDate).Days;
                var daysUntilDue = (dueDate - businessToday.Date).Days;

                if (dueDate < businessToday.Date)
                {
                    result.OverdueAmount += document.OutstandingAmount;
                    if (result.OldestOverdueDate == null || dueDate < result.OldestOverdueDate.Value)
                    {
                        result.OldestOverdueDate = dueDate;
                    }
                    result.MaxDaysOverdue = Math.Max(result.MaxDaysOverdue, daysOverdue);
                }
                else if (dueDate == businessToday.Date)
                {
                    result.DueTodayAmount += document.OutstandingAmount;
                }
                else if (daysUntilDue <= reminderLeadDays)
                {
                    result.DueSoonAmount += document.OutstandingAmount;
                }
                else
                {
                    result.NotYetDueAmount += document.OutstandingAmount;
                }

                if (result.NearestDueDate == null || dueDate < result.NearestDueDate.Value)
                {
                    result.NearestDueDate = dueDate;
                }
            }
        }

        return result;
    }

    public List<DebtDocumentAllocation> CalculateDebtDocuments(
        PartyDebt partyDebt,
        List<DebtTransaction> transactions)
    {
        var activeTx = transactions
            .Where(t => t.PartyDebtId == partyDebt.Id && !t.IsDeleted)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToList();

        var documents = new List<DebtDocumentAllocation>();
        if (partyDebt.OpeningBalance > 0m)
        {
            documents.Add(new DebtDocumentAllocation
            {
                PartyDebtId = partyDebt.Id,
                ChargeTransactionId = 0,
                RefType = "OPENING_BALANCE",
                TransactionDate = partyDebt.CreatedDate,
                Note = "Số dư đầu kỳ",
                TotalAmount = partyDebt.OpeningBalance,
                OutstandingAmount = partyDebt.OpeningBalance
            });
        }

        foreach (var tx in activeTx)
        {
            var effect = _effectResolver.GetBalanceEffect(tx.TransactionType, tx.Amount);
            if (effect <= 0m) continue;

            documents.Add(new DebtDocumentAllocation
            {
                PartyDebtId = partyDebt.Id,
                ChargeTransactionId = tx.Id,
                RefType = NormalizeRefType(tx.RefType),
                RefId = tx.RefId,
                TransactionDate = tx.TransactionDate,
                DueDate = tx.DueDate,
                Note = tx.Note,
                TotalAmount = effect,
                OutstandingAmount = effect
            });
        }

        // Nếu dữ liệu lịch sử chỉ có CurrentBalance mà thiếu giao dịch nguồn,
        // vẫn tạo một dòng đối soát để tổng chi tiết luôn khớp số dư sổ.
        var totalCharges = documents.Sum(x => x.TotalAmount);
        if (partyDebt.CurrentBalance > totalCharges)
        {
            var adjustment = partyDebt.CurrentBalance - totalCharges;
            documents.Add(new DebtDocumentAllocation
            {
                PartyDebtId = partyDebt.Id,
                ChargeTransactionId = -1,
                RefType = "BALANCE_ADJUSTMENT",
                TransactionDate = partyDebt.CreatedDate,
                Note = "Số dư chưa có chứng từ nguồn",
                TotalAmount = adjustment,
                OutstandingAmount = adjustment
            });
            totalCharges += adjustment;
        }

        var settlementTarget = Math.Max(0m, totalCharges - Math.Max(0m, partyDebt.CurrentBalance));
        if (settlementTarget == 0m || documents.Count == 0)
            return documents;

        var settlements = activeTx
            .Where(t => _effectResolver.GetBalanceEffect(t.TransactionType, t.Amount) < 0m)
            .Select(t => new
            {
                Transaction = t,
                Amount = Math.Abs(_effectResolver.GetBalanceEffect(t.TransactionType, t.Amount))
            })
            .ToList();

        decimal allocated = 0m;

        // Thanh toán có RefType + RefId được ưu tiên vào đúng chứng từ.
        foreach (var settlement in settlements)
        {
            if (allocated >= settlementTarget ||
                string.IsNullOrWhiteSpace(settlement.Transaction.RefType) ||
                !settlement.Transaction.RefId.HasValue)
                continue;

            var matching = documents
                .Where(d => d.RefId == settlement.Transaction.RefId &&
                            NormalizeRefType(d.RefType) == NormalizeRefType(settlement.Transaction.RefType))
                .OrderBy(d => d.TransactionDate)
                .ThenBy(d => d.ChargeTransactionId)
                .ToList();

            var remaining = Math.Min(settlement.Amount, settlementTarget - allocated);
            foreach (var document in matching)
            {
                var applied = Math.Min(document.OutstandingAmount, remaining);
                document.OutstandingAmount -= applied;
                document.PaidAmount += applied;
                allocated += applied;
                remaining -= applied;
                if (remaining <= 0m) break;
            }
        }

        // Phần còn lại phân bổ FIFO theo hạn thanh toán, rồi ngày phát sinh.
        var fifoRemaining = settlementTarget - allocated;
        foreach (var document in documents
                     .OrderBy(x => x.DueDate == null)
                     .ThenBy(x => x.DueDate)
                     .ThenBy(x => x.TransactionDate)
                     .ThenBy(x => x.ChargeTransactionId))
        {
            if (fifoRemaining <= 0m) break;
            var applied = Math.Min(document.OutstandingAmount, fifoRemaining);
            document.OutstandingAmount -= applied;
            document.PaidAmount += applied;
            fifoRemaining -= applied;
        }

        return documents;
    }

    private static string? NormalizeRefType(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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

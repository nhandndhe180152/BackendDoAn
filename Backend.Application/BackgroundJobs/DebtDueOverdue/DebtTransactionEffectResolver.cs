using System;
using Backend.Application.Constants;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public class DebtTransactionEffectResolver : IDebtTransactionEffectResolver
{
    public decimal GetBalanceEffect(string transactionType, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(transactionType)) return 0;

        var type = transactionType.ToUpperInvariant();

        return type switch
        {
            // Types that increase the debt balance
            LookupCodes.DebtTransactionType.Charge or
            LookupCodes.DebtTransactionType.RefundPayable or
            LookupCodes.DebtTransactionType.SaleCharge => amount,

            // Types that decrease the debt balance
            LookupCodes.DebtTransactionType.Payment or
            LookupCodes.DebtTransactionType.ReturnCredit => -amount,

            _ => 0
        };
    }
}

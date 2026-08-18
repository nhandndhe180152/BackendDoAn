namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public interface IDebtTransactionEffectResolver
{
    decimal GetBalanceEffect(string transactionType, decimal amount);
}

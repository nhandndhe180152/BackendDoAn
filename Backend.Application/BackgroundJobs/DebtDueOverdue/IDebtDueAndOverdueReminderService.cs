using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public interface IDebtDueAndOverdueReminderService
{
    Task<DebtDueOverdueJobResult> EvaluateAllDebtsAsync(CancellationToken cancellationToken);
    Task<DebtDueOverduePartyEvaluationResult> EvaluatePartyDebtAsync(int partyDebtId, CancellationToken cancellationToken);
}

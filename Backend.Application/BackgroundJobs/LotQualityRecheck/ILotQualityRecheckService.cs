using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.LotQualityRecheck;

public interface ILotQualityRecheckService
{
    Task<LotQualityRecheckJobResult> EvaluateAllLotsAsync(CancellationToken cancellationToken);
    Task<LotQualityRecheckLotEvaluationResult> EvaluateLotAsync(int lotId, CancellationToken cancellationToken);
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public interface IIntakeBottleneckEvaluationService
{
    Task<IntakeBottleneckJobResult> EvaluateAllAsync(CancellationToken cancellationToken);
    Task<IntakeBottleneckWarehouseResult> EvaluateWarehouseAsync(int warehouseId, CancellationToken cancellationToken);
}

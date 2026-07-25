using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public interface IIntakeBottleneckQueryService
{
    Task<List<int>> GetActiveWarehouseIdsAsync(CancellationToken cancellationToken);
    
    Task<decimal> GetExpectedIntakeAsync(int warehouseId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken);
    
    Task<decimal> GetFreeStorageCapacityAsync(int warehouseId, CancellationToken cancellationToken);
    
    Task<decimal?> GetLabourCapacityAsync(int warehouseId, CancellationToken cancellationToken);

    Task<(decimal? WindowHours, decimal? WarningRatio, decimal? CriticalRatio)> GetGlobalThresholdConfigsAsync(CancellationToken cancellationToken);
}

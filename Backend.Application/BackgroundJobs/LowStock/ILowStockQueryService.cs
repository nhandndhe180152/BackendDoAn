using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.LowStock;

public interface ILowStockQueryService
{
    Task<List<LowStockSnapshotDto>> GetLowStockSnapshotsAsync(CancellationToken cancellationToken);
    Task<LowStockSnapshotDto?> GetLowStockSnapshotForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken);
}

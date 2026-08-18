using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.LowStock;

public interface ILowStockDetectionService
{
    Task<LowStockJobResult> DetectLowStockAsync(CancellationToken cancellationToken);
    Task DetectLowStockForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken);
}

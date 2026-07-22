using System.Threading;
using System.Threading.Tasks;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyLotTraceabilityService
{
    Task<ApiResponse> GetByLotIdAsync(
        int lotId,
        bool includeTimeline = true,
        bool includeQuality = true,
        bool includeMilling = true,
        bool includeOutbound = true,
        int maxDepth = 10,
        CancellationToken cancellationToken = default);

    Task<ApiResponse> GetByLotCodeAsync(
        string lotCode,
        bool includeTimeline = true,
        bool includeQuality = true,
        bool includeMilling = true,
        bool includeOutbound = true,
        int maxDepth = 10,
        CancellationToken cancellationToken = default);
}

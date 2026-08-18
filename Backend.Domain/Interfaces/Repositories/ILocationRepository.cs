using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ILocationRepository : IRepositoryBase<Location, int>
{
    Task<DTResult<LocationAggregate>> GetPagedAsync(DTParameter parameters);
    Task<int> UpdateCapacitySafetyAsync(int locationId, int warehouseId, decimal weightKg, int productVariantId, bool isQuarantine, int userId);
    Task<int> TryLockForOutboundAsync(IReadOnlyCollection<int> locationIds, int outboundOrderId, DateTime lockedAt, int userId);
    Task<int> ReleaseOutboundLocksAsync(int outboundOrderId, DateTime modifiedAt, int userId, IReadOnlyCollection<int>? excludedLocationIds = null);
}

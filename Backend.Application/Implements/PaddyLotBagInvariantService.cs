using Backend.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public sealed class PaddyLotBagInvariantService : IPaddyLotBagInvariantService
{
    private const decimal ToleranceKg = 0.001m;
    private readonly IApplicationDbContext _context;

    public PaddyLotBagInvariantService(IApplicationDbContext context) => _context = context;

    public async Task ValidateBagAsync(int bagId, CancellationToken cancellationToken = default)
    {
        var bag = await _context.PaddyLotBags.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bagId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy bao #{bagId}.");
        var contentWeight = await _context.PaddyLotBagContents.AsNoTracking()
            .Where(x => x.BagId == bagId).SumAsync(x => x.WeightKg, cancellationToken);
        if (bag.WeightKg < 0 || Math.Abs(bag.WeightKg - contentWeight) > ToleranceKg)
            throw new InvalidOperationException($"Bao #{bagId} không cân bằng: trọng lượng bao {bag.WeightKg:0.###} kg, thành phần {contentWeight:0.###} kg.");
        if (bag.Status == Constants.PaddyLotBagStatuses.Stored && bag.LocationId == null)
            throw new InvalidOperationException($"Bao #{bagId} đang lưu kho nhưng chưa có vị trí.");
    }

    public async Task ValidateLotLocationAsync(int lotId, int locationId, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.PaddyLotBagContents.AsNoTracking()
            .AnyAsync(x => x.LotId == lotId && x.Bag.LocationId == locationId && x.Bag.Status == Constants.PaddyLotBagStatuses.Stored, cancellationToken);
        if (!tracked) return; // Dữ liệu cũ chưa quản lý theo bao được giữ tương thích.

        var bagWeight = await _context.PaddyLotBagContents.AsNoTracking()
            .Where(x => x.LotId == lotId && x.Bag.LocationId == locationId && x.Bag.Status == Constants.PaddyLotBagStatuses.Stored)
            .SumAsync(x => x.WeightKg, cancellationToken);
        var inventoryWeight = await _context.Inventories.AsNoTracking()
            .Where(x => x.PaddyLotId == lotId && x.LocationId == locationId)
            .SumAsync(x => x.QuantityOnHand, cancellationToken);
        if (Math.Abs(bagWeight - inventoryWeight) > ToleranceKg)
            throw new InvalidOperationException($"Lô #{lotId} tại vị trí #{locationId} lệch tồn: trong bao {bagWeight:0.###} kg, tồn kho {inventoryWeight:0.###} kg.");
    }
}

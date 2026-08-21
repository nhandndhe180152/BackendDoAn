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
        var bag = await _context.PaddyLotBags.AsNoTracking()
            .Include(x => x.Lot)
            .Include(x => x.Contents).ThenInclude(x => x.Lot)
            .SingleOrDefaultAsync(x => x.Id == bagId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy bao #{bagId}.");
        var allContents = await _context.PaddyLotBagContents.AsNoTracking()
            .Include(x => x.Lot)
            .Where(x => x.BagId == bagId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var activeContents = allContents.Where(x => x.WeightKg > 0).ToList();
        var contentWeight = activeContents.Sum(x => x.WeightKg);
        if (bag.WeightKg < 0 || Math.Abs(bag.WeightKg - contentWeight) > ToleranceKg)
            throw new InvalidOperationException($"Bao #{bagId} không cân bằng: trọng lượng bao {bag.WeightKg:0.###} kg, thành phần {contentWeight:0.###} kg.");
        if (allContents.Any(x => x.WeightKg < 0))
            throw new InvalidOperationException($"Bao #{bagId} có thành phần khối lượng âm.");
        var knownVariants = activeContents.Where(x => x.Lot != null).Select(x => x.Lot.ProductVariantId).Distinct().ToList();
        if (knownVariants.Count > 1 || (bag.Lot != null && knownVariants.Any(x => x != bag.Lot.ProductVariantId)))
            throw new InvalidOperationException($"Bao #{bagId} có thành phần khác SKU với lô đại diện.");
        if (bag.WeightKg > ToleranceKg && activeContents.All(x => x.LotId != bag.LotId))
            throw new InvalidOperationException($"Bao #{bagId} có lô đại diện không còn thành phần; cần chọn lại lô đại diện.");
        if (bag.Status == Constants.PaddyLotBagStatuses.Stored && bag.LocationId == null)
            throw new InvalidOperationException($"Bao #{bagId} đang lưu kho nhưng chưa có vị trí.");
        if (bag.Status == Constants.PaddyLotBagStatuses.Stored && !bag.IsFull && bag.LocationId.HasValue)
        {
            var topOrder = await _context.PaddyLotBags.AsNoTracking()
                .Where(x => x.LocationId == bag.LocationId && x.Status == Constants.PaddyLotBagStatuses.Stored && !x.IsDeleted)
                .MaxAsync(x => (int?)x.StackOrder, cancellationToken) ?? 0;
            if (bag.StackOrder != topOrder)
                throw new InvalidOperationException($"Bao mở #{bagId} không nằm trên đỉnh cột.");
        }
    }

    public async Task ValidateLotLocationAsync(int lotId, int locationId, CancellationToken cancellationToken = default)
    {
        var partialBagCount = await _context.PaddyLotBags.AsNoTracking()
            .CountAsync(x => x.LocationId == locationId
                && x.Status == Constants.PaddyLotBagStatuses.Stored
                && !x.IsDeleted
                && !x.IsFull,
                cancellationToken);
        if (partialBagCount > 1)
            throw new InvalidOperationException(
                $"Vị trí #{locationId} có {partialBagCount} bao lẻ; mỗi vị trí chỉ được có tối đa một bao lẻ.");

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

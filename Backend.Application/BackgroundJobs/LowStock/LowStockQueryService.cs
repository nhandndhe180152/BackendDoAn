using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.BackgroundJobs.LowStock;

public class LowStockQueryService : ILowStockQueryService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LowStockQueryService> _logger;

    public LowStockQueryService(IApplicationDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<LowStockQueryService>();
    }

    public async Task<(List<LowStockSnapshotDto> Snapshots, int SkippedCount)> GetLowStockSnapshotsAsync(CancellationToken cancellationToken)
    {
        // 1. Lấy aggregation tồn kho từ DB (chỉ các dòng còn tồn tại và hợp lệ)
        //    Chú ý: đây chỉ là phần "có dòng Inventory" — các SKU hết sạch sẽ KHÔNG có trong dict này (AvailableKg mặc định = 0)
        var rawInventories = await _context.Inventories
            .AsNoTracking()
            .Where(i => !i.IsDeleted)
            .Where(i => i.Warehouse.IsActive && !i.Warehouse.IsDeleted)
            .Where(i => i.ProductVariant.IsActive && !i.ProductVariant.IsDeleted)
            .Where(i => i.Location == null || (i.Location.IsActive && !i.Location.IsDeleted && !i.Location.IsQuarantine))
            .Where(i => i.PaddyLot == null || (i.PaddyLot.Status.IsSellable && !i.PaddyLot.IsDeleted))
            .GroupBy(i => new { i.WarehouseId, i.ProductVariantId })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.ProductVariantId,
                SellableOnHandKg = g.Sum(x => x.QuantityOnHand),
                ReservedKg = g.Sum(x => x.QuantityReserved)
            })
            .ToListAsync(cancellationToken);

        // Build dictionary (WarehouseId, ProductVariantId) → (onHand, reserved)
        var inventoryMap = rawInventories.ToDictionary(
            r => (r.WarehouseId, r.ProductVariantId),
            r => (r.SellableOnHandKg, r.ReservedKg));

        // 2. Load StockAlertConfig active, chỉ lấy config có ProductVariantId (theo từng SKU + kho cụ thể)
        //    E2 Fix: Dùng GroupBy+Min thay vì ToDictionaryAsync để an toàn khi có config trùng (tránh ArgumentException)
        var configs = (await _context.StockAlertConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted && c.ProductVariantId != null)
            .Select(c => new { c.WarehouseId, ProductVariantId = c.ProductVariantId!.Value, c.MinThreshold })
            .ToListAsync(cancellationToken))
            .GroupBy(c => (c.WarehouseId, c.ProductVariantId))
            .ToDictionary(g => g.Key, g => g.Min(x => x.MinThreshold)); // lấy ngưỡng thấp nhất nếu trùng

        // 3. Load active Warehouses
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive && !w.IsDeleted)
            .ToDictionaryAsync(w => w.Id, w => w.Code, cancellationToken);

        // 4. Load active ProductVariants (SKU, ProductName, MinStockLevel)
        var productVariants = await _context.ProductVariants
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.SKU,
                ProductName = p.Product.Name,
                p.MinStockLevel
            })
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

        var result = new List<LowStockSnapshotDto>();
        int skippedCount = 0;

        // 5. E1 Fix: Duyệt qua TẤT CẢ (kho, SKU) có ngưỡng — không chỉ các cặp có dòng Inventory
        //    Lý do: SKU đã hết sạch (không còn dòng Inventory nào) vẫn phải sinh alert CRITICAL
        //    Ta lấy union của: (a) các cặp có dòng Inventory, (b) các cặp có StockAlertConfig (kể cả 0 dòng Inventory)

        // Tập hợp tất cả (warehouseId, productVariantId) cần kiểm tra
        var allPairs = new HashSet<(int WarehouseId, int ProductVariantId)>();

        // Các cặp có dòng inventory
        foreach (var raw in rawInventories)
            allPairs.Add((raw.WarehouseId, raw.ProductVariantId));

        // Các cặp có StockAlertConfig (SKU hết sạch vẫn cần kiểm tra)
        foreach (var cfg in configs.Keys)
            allPairs.Add(cfg);

        // Các cặp (kho active, SKU có MinStockLevel) — SKU hết sạch nhưng vẫn có ngưỡng mặc định
        foreach (var variant in productVariants.Values.Where(v => v.MinStockLevel != null))
        {
            foreach (var warehouseId in warehouses.Keys)
                allPairs.Add((warehouseId, variant.Id));
        }

        foreach (var (warehouseId, productVariantId) in allPairs)
        {
            if (!warehouses.TryGetValue(warehouseId, out var warehouseCode))
                continue;

            if (!productVariants.TryGetValue(productVariantId, out var variant))
                continue;

            // Xác định ngưỡng (ưu tiên StockAlertConfig trước, fallback sang MinStockLevel)
            decimal? threshold = null;
            if (configs.TryGetValue((warehouseId, productVariantId), out var configThreshold))
                threshold = configThreshold;
            else if (variant.MinStockLevel != null)
                threshold = variant.MinStockLevel.Value;

            if (threshold == null)
            {
                skippedCount++;
                _logger.LogInformation("[LowStockQuery] Bỏ qua SKU {SKU} tại kho {WarehouseCode}: reason = no_threshold", variant.SKU, warehouseCode);
                continue;
            }

            // Lấy tồn kho — nếu không có dòng Inventory nào → 0 kg (E1 Fix)
            inventoryMap.TryGetValue((warehouseId, productVariantId), out var inv);
            var sellableOnHand = inv.SellableOnHandKg;
            var reserved = inv.ReservedKg;
            var available = Math.Max(0m, sellableOnHand - reserved);

            result.Add(new LowStockSnapshotDto
            {
                WarehouseId = warehouseId,
                WarehouseCode = warehouseCode,
                ProductVariantId = productVariantId,
                SKU = variant.SKU,
                ProductName = variant.ProductName,
                SellableOnHandKg = sellableOnHand,
                ReservedKg = reserved,
                AvailableKg = available,
                ThresholdKg = threshold.Value
            });
        }

        return (result, skippedCount);
    }


    public async Task<LowStockSnapshotDto?> GetLowStockSnapshotForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken)
    {
        // 1. Group & Sum riêng cho warehouse + productVariant cụ thể (Chỉ group theo ID)
        var raw = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && i.ProductVariantId == productVariantId)
            .Where(i => !i.IsDeleted)
            .Where(i => i.Warehouse.IsActive && !i.Warehouse.IsDeleted)
            .Where(i => i.ProductVariant.IsActive && !i.ProductVariant.IsDeleted)
            .Where(i => i.Location == null || (i.Location.IsActive && !i.Location.IsDeleted && !i.Location.IsQuarantine))
            .Where(i => i.PaddyLot == null || (i.PaddyLot.Status.IsSellable && !i.PaddyLot.IsDeleted))
            .GroupBy(i => new
            {
                i.WarehouseId,
                i.ProductVariantId
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.ProductVariantId,
                SellableOnHandKg = g.Sum(x => x.QuantityOnHand),
                ReservedKg = g.Sum(x => x.QuantityReserved)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Fetch thông tin metadata Warehouse
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.IsActive && !w.IsDeleted, cancellationToken);

        // Fetch thông tin metadata ProductVariant và Product
        var variant = await _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == productVariantId && v.IsActive && !v.IsDeleted, cancellationToken);

        if (warehouse == null || variant == null) return null;

        decimal sellableOnHandKg = 0m;
        decimal reservedKg = 0m;

        if (raw != null)
        {
            sellableOnHandKg = raw.SellableOnHandKg;
            reservedKg = raw.ReservedKg;
        }

        // 2. Load Config ngưỡng cảnh báo
        var config = await _context.StockAlertConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.WarehouseId == warehouseId && c.ProductVariantId == productVariantId && c.IsActive && !c.IsDeleted, cancellationToken);

        decimal? threshold = null;
        if (config != null)
        {
            threshold = config.MinThreshold;
        }
        else
        {
            threshold = variant.MinStockLevel;
        }

        if (threshold == null)
        {
            _logger.LogInformation("[LowStockQuery] Bỏ qua SKU {SKU} tại kho {WarehouseId}: reason = no_threshold", variant.SKU, warehouseId);
            return null;
        }

        return new LowStockSnapshotDto
        {
            WarehouseId = warehouseId,
            WarehouseCode = warehouse.Code,
            ProductVariantId = productVariantId,
            SKU = variant.SKU,
            ProductName = variant.Product.Name,
            SellableOnHandKg = sellableOnHandKg,
            ReservedKg = reservedKg,
            AvailableKg = Math.Max(0m, sellableOnHandKg - reservedKg),
            ThresholdKg = threshold.Value
        };
    }
}

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
    private readonly IInventoryStateAggregationService _aggregationService;
    private readonly ILogger<LowStockQueryService> _logger;

    public LowStockQueryService(IApplicationDbContext context, IInventoryStateAggregationService aggregationService, ILoggerFactory loggerFactory)
    {
        _context = context;
        _aggregationService = aggregationService;
        _logger = loggerFactory.CreateLogger<LowStockQueryService>();
    }

    public async Task<(List<LowStockSnapshotDto> Snapshots, int SkippedCount)> GetLowStockSnapshotsAsync(CancellationToken cancellationToken)
    {
        // 1. Lấy toàn bộ aggregates từ aggregation service
        var aggregates = await _aggregationService.GetAggregatesAsync(null, null, cancellationToken);

        // Group by WarehouseId and ProductVariantId to match job logic
        var inventoryMap = aggregates
            .GroupBy(a => (a.WarehouseId, a.ProductVariantId))
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    SellableOnHandKg = g.Sum(x => x.SellableOnHandKg),
                    ReservedSellableKg = g.Sum(x => x.ReservedSellableKg)
                });

        // 2. Load StockAlertConfig active, chỉ lấy config có ProductVariantId (theo từng SKU + kho cụ thể)
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

        // Tập hợp tất cả (warehouseId, productVariantId) cần kiểm tra
        var allPairs = new HashSet<(int WarehouseId, int ProductVariantId)>();

        // Các cặp từ aggregates
        foreach (var agg in aggregates)
            allPairs.Add((agg.WarehouseId, agg.ProductVariantId));

        // Các cặp có StockAlertConfig
        foreach (var cfg in configs.Keys)
            allPairs.Add(cfg);

        // Các cặp (kho active, SKU có MinStockLevel)
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

            // Xác định ngưỡng
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

            decimal sellableOnHand = 0m;
            decimal reservedSellable = 0m;

            if (inventoryMap.TryGetValue((warehouseId, productVariantId), out var inv))
            {
                sellableOnHand = inv.SellableOnHandKg;
                reservedSellable = inv.ReservedSellableKg;
            }

            var available = Math.Max(0m, sellableOnHand - reservedSellable);

            result.Add(new LowStockSnapshotDto
            {
                WarehouseId = warehouseId,
                WarehouseCode = warehouseCode,
                ProductVariantId = productVariantId,
                SKU = variant.SKU,
                ProductName = variant.ProductName,
                SellableOnHandKg = sellableOnHand,
                ReservedKg = reservedSellable,
                AvailableKg = available,
                ThresholdKg = threshold.Value
            });
        }

        return (result, skippedCount);
    }

    public async Task<LowStockSnapshotDto?> GetLowStockSnapshotForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken)
    {
        var aggregates = await _aggregationService.GetAggregatesAsync(warehouseId, productVariantId, cancellationToken);

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

        decimal sellableOnHandKg = aggregates.Sum(x => x.SellableOnHandKg);
        decimal reservedSellableKg = aggregates.Sum(x => x.ReservedSellableKg);

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
            ReservedKg = reservedSellableKg,
            AvailableKg = Math.Max(0m, sellableOnHandKg - reservedSellableKg),
            ThresholdKg = threshold.Value
        };
    }
}

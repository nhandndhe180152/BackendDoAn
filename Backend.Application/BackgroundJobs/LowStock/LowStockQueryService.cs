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
        // 1. Group & Sum ở database level bằng AsNoTracking (Chỉ group theo ID để tối ưu hoá và tránh lỗi dịch GroupBy trên MySQL)
        var rawInventories = await _context.Inventories
            .AsNoTracking()
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
            .ToListAsync(cancellationToken);

        // 2. Load các cấu hình ngưỡng cảnh báo (StockAlertConfig) active
        var configs = await _context.StockAlertConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted && c.ProductVariantId != null)
            .ToDictionaryAsync(c => (c.WarehouseId, c.ProductVariantId!.Value), c => c.MinThreshold, cancellationToken);

        // 3. Load active Warehouses thông tin Code
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

        foreach (var raw in rawInventories)
        {
            if (!warehouses.TryGetValue(raw.WarehouseId, out var warehouseCode))
                continue; // Kho bị xoá hoặc không hoạt động

            if (!productVariants.TryGetValue(raw.ProductVariantId, out var variant))
                continue; // Biến thể bị xoá hoặc không hoạt động

            decimal? threshold = null;

            // Ưu tiên 1: StockAlertConfig của Warehouse + ProductVariant
            if (configs.TryGetValue((raw.WarehouseId, raw.ProductVariantId), out var configThreshold))
            {
                threshold = configThreshold;
            }
            // Ưu tiên 2: ProductVariant.MinStockLevel
            else if (variant.MinStockLevel != null)
            {
                threshold = variant.MinStockLevel.Value;
            }

            if (threshold == null)
            {
                skippedCount++;
                _logger.LogInformation("[LowStockQuery] Bỏ qua SKU {SKU} tại kho {WarehouseCode}: reason = no_threshold", variant.SKU, warehouseCode);
                continue;
            }

            result.Add(new LowStockSnapshotDto
            {
                WarehouseId = raw.WarehouseId,
                WarehouseCode = warehouseCode,
                ProductVariantId = raw.ProductVariantId,
                SKU = variant.SKU,
                ProductName = variant.ProductName,
                SellableOnHandKg = raw.SellableOnHandKg,
                ReservedKg = raw.ReservedKg,
                AvailableKg = Math.Max(0m, raw.SellableOnHandKg - raw.ReservedKg),
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

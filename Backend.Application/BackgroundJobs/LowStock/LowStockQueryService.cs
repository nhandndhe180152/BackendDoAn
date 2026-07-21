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

    public async Task<List<LowStockSnapshotDto>> GetLowStockSnapshotsAsync(CancellationToken cancellationToken)
    {
        // 1. Group & Sum ở database level bằng AsNoTracking
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
                WarehouseCode = i.Warehouse.Code,
                i.ProductVariantId,
                i.ProductVariant.SKU,
                ProductName = i.ProductVariant.Product.Name
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.WarehouseCode,
                g.Key.ProductVariantId,
                g.Key.SKU,
                g.Key.ProductName,
                SellableOnHandKg = g.Sum(x => x.QuantityOnHand),
                ReservedKg = g.Sum(x => x.QuantityReserved)
            })
            .ToListAsync(cancellationToken);

        // 2. Load các cấu hình ngưỡng cảnh báo (StockAlertConfig) active
        var configs = await _context.StockAlertConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted && c.ProductVariantId != null)
            .ToDictionaryAsync(c => (c.WarehouseId, c.ProductVariantId!.Value), c => c.MinThreshold, cancellationToken);

        // 3. Load MinStockLevel của ProductVariant
        var productVariants = await _context.ProductVariants
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, p => p.MinStockLevel, cancellationToken);

        var result = new List<LowStockSnapshotDto>();

        foreach (var raw in rawInventories)
        {
            decimal? threshold = null;

            // Ưu tiên 1: StockAlertConfig của Warehouse + ProductVariant
            if (configs.TryGetValue((raw.WarehouseId, raw.ProductVariantId), out var configThreshold))
            {
                threshold = configThreshold;
            }
            // Ưu tiên 2: ProductVariant.MinStockLevel
            else if (productVariants.TryGetValue(raw.ProductVariantId, out var variantThresholdVal) && variantThresholdVal != null)
            {
                threshold = variantThresholdVal.Value;
            }

            if (threshold == null)
            {
                _logger.LogInformation("[LowStockQuery] Bỏ qua SKU {SKU} tại kho {WarehouseCode}: reason = no_threshold", raw.SKU, raw.WarehouseCode);
                continue;
            }

            result.Add(new LowStockSnapshotDto
            {
                WarehouseId = raw.WarehouseId,
                WarehouseCode = raw.WarehouseCode,
                ProductVariantId = raw.ProductVariantId,
                SKU = raw.SKU,
                ProductName = raw.ProductName,
                SellableOnHandKg = raw.SellableOnHandKg,
                ReservedKg = raw.ReservedKg,
                AvailableKg = Math.Max(0m, raw.SellableOnHandKg - raw.ReservedKg),
                ThresholdKg = threshold.Value
            });
        }

        return result;
    }

    public async Task<LowStockSnapshotDto?> GetLowStockSnapshotForProductAsync(int warehouseId, int productVariantId, CancellationToken cancellationToken)
    {
        // 1. Group & Sum riêng cho warehouse + productVariant cụ thể
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
                WarehouseCode = i.Warehouse.Code,
                i.ProductVariantId,
                i.ProductVariant.SKU,
                ProductName = i.ProductVariant.Product.Name
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.WarehouseCode,
                g.Key.ProductVariantId,
                g.Key.SKU,
                g.Key.ProductName,
                SellableOnHandKg = g.Sum(x => x.QuantityOnHand),
                ReservedKg = g.Sum(x => x.QuantityReserved)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw == null)
        {
            // Trả về snapshot 0 kg nếu không có inventory record
            var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId && w.IsActive && !w.IsDeleted, cancellationToken);
            var variant = await _context.ProductVariants.AsNoTracking().Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == productVariantId && v.IsActive && !v.IsDeleted, cancellationToken);
            if (warehouse == null || variant == null) return null;

            raw = new
            {
                WarehouseId = warehouse.Id,
                WarehouseCode = warehouse.Code,
                ProductVariantId = variant.Id,
                SKU = variant.SKU,
                ProductName = variant.Product.Name,
                SellableOnHandKg = 0m,
                ReservedKg = 0m
            };
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
            var variant = await _context.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == productVariantId && v.IsActive && !v.IsDeleted, cancellationToken);
            if (variant != null)
            {
                threshold = variant.MinStockLevel;
            }
        }

        if (threshold == null)
        {
            _logger.LogInformation("[LowStockQuery] Bỏ qua SKU {SKU} tại kho {WarehouseId}: reason = no_threshold", raw.SKU, raw.WarehouseId);
            return null;
        }

        return new LowStockSnapshotDto
        {
            WarehouseId = raw.WarehouseId,
            WarehouseCode = raw.WarehouseCode,
            ProductVariantId = raw.ProductVariantId,
            SKU = raw.SKU,
            ProductName = raw.ProductName,
            SellableOnHandKg = raw.SellableOnHandKg,
            ReservedKg = raw.ReservedKg,
            AvailableKg = Math.Max(0m, raw.SellableOnHandKg - raw.ReservedKg),
            ThresholdKg = threshold.Value
        };
    }
}

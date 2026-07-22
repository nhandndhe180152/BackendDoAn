using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class InventoryStateAggregationService : IInventoryStateAggregationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<InventoryStateAggregationService> _logger;

    public InventoryStateAggregationService(IApplicationDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<InventoryStateAggregationService>();
    }

    public async Task<List<InventoryStateAggregateDto>> GetAggregatesAsync(int? warehouseId, int? productVariantId, CancellationToken cancellationToken)
    {
        var query = _context.Inventories
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }
        if (productVariantId.HasValue)
        {
            query = query.Where(x => x.ProductVariantId == productVariantId.Value);
        }

        // Aggregate at DB level (grouping by key properties)
        var aggregates = await query
            .GroupBy(x => new
            {
                x.WarehouseId,
                WarehouseCode = x.Warehouse.Code,
                WarehouseName = x.Warehouse.Name,
                x.ProductVariantId,
                x.ProductVariant.SKU,
                ProductName = x.ProductVariant.Product.Name,
                ProductVariantName = x.ProductVariant.Name,
                x.PaddyLotId,
                LotCode = x.PaddyLot != null ? x.PaddyLot.LotCode : null,
                LotType = x.PaddyLot != null ? x.PaddyLot.LotType : null,
                LotStatusName = x.PaddyLot != null ? x.PaddyLot.Status.Name : null,
                LotStatusCode = x.PaddyLot != null ? x.PaddyLot.Status.Code : null,
                LotStatusIsSellable = x.PaddyLot != null ? (bool?)x.PaddyLot.Status.IsSellable : null,
                RiceVarietyId = x.PaddyLot != null ? x.PaddyLot.RiceVarietyId : x.ProductVariant.RiceVarietyId,
                IsVariantByproduct = x.ProductVariant.IsByproduct,
                x.LocationId,
                LocationIsQuarantine = x.Location != null && x.Location.IsQuarantine,
                LocationCode = x.Location == null
                    ? null
                    : (string.IsNullOrEmpty(x.Location.ZoneName) ? "" : x.Location.ZoneName)
                        + (string.IsNullOrEmpty(x.Location.ShelfRow) ? "" : "-" + x.Location.ShelfRow)
                        + (string.IsNullOrEmpty(x.Location.ShelfLevel) ? "" : "-" + x.Location.ShelfLevel)
                        + (string.IsNullOrEmpty(x.Location.SlotCode) ? "" : "-" + x.Location.SlotCode),
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.WarehouseCode,
                g.Key.WarehouseName,
                g.Key.ProductVariantId,
                g.Key.SKU,
                g.Key.ProductName,
                g.Key.ProductVariantName,
                g.Key.PaddyLotId,
                g.Key.LotCode,
                g.Key.LotType,
                g.Key.LotStatusName,
                g.Key.LotStatusCode,
                g.Key.LotStatusIsSellable,
                g.Key.RiceVarietyId,
                g.Key.IsVariantByproduct,
                g.Key.LocationId,
                g.Key.LocationIsQuarantine,
                g.Key.LocationCode,
                QuantityOnHand = g.Sum(x => x.QuantityOnHand),
                QuantityReserved = g.Sum(x => x.QuantityReserved)
            })
            .ToListAsync(cancellationToken);

        var result = new List<InventoryStateAggregateDto>();

        foreach (var item in aggregates)
        {
            var isQuarantined = item.LocationIsQuarantine || item.LotStatusCode == LotStatusCodeConstants.Quarantine;
            var isSellable = !isQuarantined && (item.PaddyLotId == null || item.LotStatusIsSellable == true);
            var otherBlocked = !isQuarantined && !isSellable;

            var quarantinedKg = isQuarantined ? item.QuantityOnHand : 0m;
            var sellableOnHandKg = isSellable ? item.QuantityOnHand : 0m;
            var otherBlockedKg = otherBlocked ? item.QuantityOnHand : 0m;

            var reservedSellableKg = isSellable ? item.QuantityReserved : 0m;
            var reservedQuarantinedKg = isQuarantined ? item.QuantityReserved : 0m;

            // Log quarantine reserved stock anomaly (Section 5 & 20)
            if (isQuarantined && item.QuantityReserved > 0)
            {
                _logger.LogWarning("[QuarantineAnomaly] Reserved quarantined stock detected: {ReservedQuarantined} kg in warehouse {WarehouseId}, product variant {ProductVariantId}, lot {LotCode}.", 
                    item.QuantityReserved, item.WarehouseId, item.ProductVariantId, item.LotCode);
            }

            var availableKg = Math.Max(0m, sellableOnHandKg - reservedSellableKg);

            result.Add(new InventoryStateAggregateDto
            {
                WarehouseId = item.WarehouseId,
                WarehouseCode = item.WarehouseCode,
                WarehouseName = item.WarehouseName,
                ProductVariantId = item.ProductVariantId,
                SKU = item.SKU,
                ProductName = item.ProductName,
                ProductVariantName = item.ProductVariantName,
                PaddyLotId = item.PaddyLotId,
                LotCode = item.LotCode,
                LotType = item.LotType,
                LotStatusName = item.LotStatusName,
                RiceVarietyId = item.RiceVarietyId,
                IsVariantByproduct = item.IsVariantByproduct,
                LocationId = item.LocationId,
                LocationCode = item.LocationCode,
                TotalOnHandKg = item.QuantityOnHand,
                SellableOnHandKg = sellableOnHandKg,
                ReservedKg = item.QuantityReserved,
                ReservedSellableKg = reservedSellableKg,
                QuarantinedKg = quarantinedKg,
                OtherBlockedKg = otherBlockedKg,
                AvailableKg = availableKg
            });
        }

        return result;
    }
}

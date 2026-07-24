using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public class IntakeBottleneckQueryService : IIntakeBottleneckQueryService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<IntakeBottleneckQueryService> _logger;
    private readonly ISystemLookup _systemLookup;

    public IntakeBottleneckQueryService(
        IApplicationDbContext context,
        ILoggerFactory loggerFactory,
        ISystemLookup systemLookup)
    {
        _context = context;
        _logger = loggerFactory.CreateLogger<IntakeBottleneckQueryService>();
        _systemLookup = systemLookup;
    }

    public async Task<List<int>> GetActiveWarehouseIdsAsync(CancellationToken cancellationToken)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive && !w.IsDeleted)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetExpectedIntakeAsync(int warehouseId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken)
    {
        // 1. Phân giải các status Id cần thiết từ ISystemLookup
        int confirmedStatusId = _systemLookup.PaddyScheduleStatusId(IntakeBottleneckConstants.ScheduleStatus.Confirmed);
        int collectingStatusId = _systemLookup.PaddyScheduleStatusId(IntakeBottleneckConstants.ScheduleStatus.Collecting);

        // 2. Query expected schedules
        var schedules = await _context.PaddyPurchaseSchedules
            .AsNoTracking()
            .Where(s => s.WarehouseId == warehouseId &&
                        !s.IsDeleted &&
                        (s.StatusId == confirmedStatusId || s.StatusId == collectingStatusId) &&
                        s.ScheduleDate >= windowStart &&
                        s.ScheduleDate < windowEnd)
            .Select(s => new 
            { 
                s.EstimatedQtyKg,
                ReceivedQty = s.PaddyPurchaseReceipts.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.ActualWeightKg) ?? 0m
            })
            .ToListAsync(cancellationToken);

        decimal totalExpected = 0;
        foreach (var sch in schedules)
        {
            if (sch.EstimatedQtyKg.HasValue && sch.EstimatedQtyKg.Value > 0)
            {
                var remaining = Math.Max(0, sch.EstimatedQtyKg.Value - sch.ReceivedQty);
                totalExpected += remaining;
            }
            else
            {
                _logger.LogDebug("[IntakeBottleneck] Schedule skipped. Reason: invalid_estimated_quantity, Value: {Qty}", sch.EstimatedQtyKg);
            }
        }

        return totalExpected;
    }

    public async Task<decimal> GetFreeStorageCapacityAsync(int warehouseId, CancellationToken cancellationToken)
    {
        // Category 101 đại diện cho "Lúa thô" (Paddy)
        int PaddyCategoryId = CommonConstants.ProductCategory.Paddy;

        var locations = await _context.Locations
            .AsNoTracking()
            .Where(l => l.WarehouseId == warehouseId && l.IsActive && !l.IsDeleted && !l.IsQuarantine)
            .Select(l => new
            {
                l.Id,
                l.MaxCapacity,
                l.CurrentOccupancy,
                l.AllowedCategoryId,
                l.CurrentProductVariantId,
                l.IsSingleTypeColumn,
                ProductCategoryId = l.CurrentProductVariant != null ? (int?)l.CurrentProductVariant.Product.ProductCategoryId : null
            })
            .ToListAsync(cancellationToken);

        decimal freeCapacitySum = 0;
        foreach (var loc in locations)
        {
            // 1. Cột không tương thích không được tính (Chỉ tính allowedCategory là Lúa thô hoặc null)
            if (loc.AllowedCategoryId.HasValue && loc.AllowedCategoryId.Value != PaddyCategoryId)
            {
                _logger.LogDebug("[IntakeBottleneck] Location {Id} skipped. Reason: incompatible_category", loc.Id);
                continue;
            }

            // 2. Nếu là SingleTypeColumn và đang chứa gạo hoặc phụ phẩm thì không được nhận lúa thô
            if (loc.IsSingleTypeColumn && loc.CurrentOccupancy > 0 && loc.CurrentProductVariantId.HasValue)
            {
                if (loc.ProductCategoryId.HasValue && loc.ProductCategoryId.Value != PaddyCategoryId)
                {
                    _logger.LogDebug("[IntakeBottleneck] Location {Id} skipped. Reason: incompatible_stored_product_variant", loc.Id);
                    continue;
                }
            }

            // 3. MaxCapacity null hoặc <= 0
            if (loc.MaxCapacity == null || loc.MaxCapacity.Value <= 0)
            {
                _logger.LogWarning("[IntakeBottleneck] Location {Id} skipped. Reason: invalid_max_capacity", loc.Id);
                continue;
            }

            // 4. CurrentOccupancy < 0
            if (loc.CurrentOccupancy < 0)
            {
                _logger.LogWarning("[IntakeBottleneck] Location {Id} has negative occupancy: {Occupancy}", loc.Id, loc.CurrentOccupancy);
            }

            // 5. CurrentOccupancy > MaxCapacity
            if (loc.CurrentOccupancy > loc.MaxCapacity.Value)
            {
                _logger.LogWarning("[IntakeBottleneck] Location {Id} is over capacity. Max: {Max}, Current: {Current}", loc.Id, loc.MaxCapacity.Value, loc.CurrentOccupancy);
                continue; // free capacity = 0, skip
            }

            var locFree = Math.Max(0, loc.MaxCapacity.Value - loc.CurrentOccupancy);
            freeCapacitySum += locFree;
        }

        return freeCapacitySum;
    }

    public async Task<decimal?> GetLabourCapacityAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var key = $"{IntakeBottleneckConstants.ConfigKey.LabourCapacityPrefix}{warehouseId}";
        var configVal = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.ConfigKey == key && !c.IsDeleted)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configVal))
        {
            return null;
        }

        if (decimal.TryParse(configVal, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedVal))
        {
            return parsedVal;
        }

        return null;
    }

    public async Task<(decimal? WindowHours, decimal? WarningRatio, decimal? CriticalRatio)> GetGlobalThresholdConfigsAsync(CancellationToken cancellationToken)
    {
        var configs = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => (c.ConfigKey == IntakeBottleneckConstants.ConfigKey.WindowHours ||
                         c.ConfigKey == IntakeBottleneckConstants.ConfigKey.WarningRatio ||
                         c.ConfigKey == IntakeBottleneckConstants.ConfigKey.CriticalRatio) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

        decimal? windowHours = null;
        decimal? warningRatio = null;
        decimal? criticalRatio = null;

        if (configs.TryGetValue(IntakeBottleneckConstants.ConfigKey.WindowHours, out var whStr) &&
            decimal.TryParse(whStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var whParsed))
        {
            windowHours = whParsed;
        }

        if (configs.TryGetValue(IntakeBottleneckConstants.ConfigKey.WarningRatio, out var wrStr) &&
            decimal.TryParse(wrStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var wrParsed))
        {
            warningRatio = wrParsed;
        }

        if (configs.TryGetValue(IntakeBottleneckConstants.ConfigKey.CriticalRatio, out var crStr) &&
            decimal.TryParse(crStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var crParsed))
        {
            criticalRatio = crParsed;
        }

        return (windowHours, warningRatio, criticalRatio);
    }
}

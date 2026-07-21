using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Putaway;
using Backend.Application.Interfaces;
using Backend.Application.BackgroundJobs.LowStock;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Backend.Share.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class PutawaySuggestionService : IPutawaySuggestionService
{
    private readonly IApplicationDbContext _context;
    private readonly ILocationRepository _locationRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IScheduledJobService _scheduledJobService;
    private readonly ILogger<PutawaySuggestionService> _logger;

    public PutawaySuggestionService(
        IApplicationDbContext context,
        ILocationRepository locationRepository,
        IHttpContextAccessor httpContextAccessor,
        IScheduledJobService scheduledJobService,
        ILogger<PutawaySuggestionService> logger)
    {
        _context = context;
        _locationRepository = locationRepository;
        _httpContextAccessor = httpContextAccessor;
        _scheduledJobService = scheduledJobService;
        _logger = logger;
    }

    private async Task<decimal> ResolveConfigDecimalAsync(string key, decimal defaultValue, int? warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId.HasValue)
        {
            var whKey = $"{key}:{warehouseId.Value}";
            var whConfig = await _context.SystemConfigs
                .AsNoTracking()
                .Where(x => x.ConfigKey == whKey && !x.IsDeleted)
                .Select(x => x.ConfigValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(whConfig) && decimal.TryParse(whConfig, out var whValue))
            {
                return whValue;
            }
        }

        var globalConfig = await _context.SystemConfigs
            .AsNoTracking()
            .Where(x => x.ConfigKey == key && !x.IsDeleted)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(globalConfig) && decimal.TryParse(globalConfig, out var globalValue))
        {
            return globalValue;
        }

        return defaultValue;
    }

    private async Task SaveConfigValueAsync(string key, string value, int? warehouseId, CancellationToken cancellationToken)
    {
        var finalKey = warehouseId.HasValue ? $"{key}:{warehouseId.Value}" : key;
        var config = await _context.SystemConfigs
            .Where(x => x.ConfigKey == finalKey && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        if (config == null)
        {
            config = new SystemConfig
            {
                ConfigKey = finalKey,
                ConfigValue = value,
                CreatedBy = userId,
                CreatedDate = now
            };
            _context.SystemConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = value;
            config.LastModifiedDate = now;
            config.UpdatedBy = userId;
            _context.SystemConfigs.Update(config);
        }
    }

    private int GetCurrentUserId()
    {
        var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out var id) ? id : 1; // Default to Admin ID (1) if context is missing
    }

    public async Task<ApiResponse> GetSuggestionsAsync(GetPutawaySuggestionsRequest request, CancellationToken cancellationToken)
    {
        if (request.RequiredWeightKg <= 0)
        {
            return ApiResponse.BadRequest("Khối lượng cần lưu phải lớn hơn 0.", ApiCodeConstants.Common.BadRequest);
        }

        var product = await _context.ProductVariants
            .AsNoTracking()
            .Where(x => x.Id == request.ProductVariantId && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                ProductCategoryId = x.Product.ProductCategoryId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            return ApiResponse.NotFound("Không tìm thấy loại sản phẩm.", ApiCodeConstants.Common.NotFound);
        }

        // Resolving weights from SystemConfig
        var capacityWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_CAPACITY_FIT_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_CAPACITY_FIT_WEIGHT,
            request.WarehouseId,
            cancellationToken);

        var occupancyWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_OCCUPANCY_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_OCCUPANCY_WEIGHT,
            request.WarehouseId,
            cancellationToken);

        var categoryWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_CATEGORY_MATCH_WEIGHT,
            request.WarehouseId,
            cancellationToken);

        var priorityWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_PRIORITY_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_PRIORITY_WEIGHT,
            request.WarehouseId,
            cancellationToken);

        var sameProductScore = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_SAME_PRODUCT_SCORE_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_SAME_PRODUCT_SCORE,
            request.WarehouseId,
            cancellationToken);

        var emptyColumnScore = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_EMPTY_COLUMN_SCORE_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_EMPTY_COLUMN_SCORE,
            request.WarehouseId,
            cancellationToken);

        var totalWeight = capacityWeight + occupancyWeight + categoryWeight + priorityWeight;
        if (Math.Abs(totalWeight - 1m) > 0.0001m)
        {
            return ApiResponse.UnprocessableEntity("Tổng bốn trọng số gợi ý vị trí phải bằng 1.", "PUTAWAY_WEIGHT_SUM_INVALID");
        }

        var requireQuarantine = request.PlacementMode == PutawayPlacementMode.Quarantine;

        // 1. Tìm các candidate có thể chứa TOÀN BỘ khối lượng yêu cầu
        var candidates = await _context.Locations
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.WarehouseId == request.WarehouseId &&
                x.IsQuarantine == requireQuarantine &&
                x.MaxCapacity != null &&
                x.MaxCapacity > 0 &&
                x.CurrentOccupancy + request.RequiredWeightKg <= x.MaxCapacity &&
                (
                    x.AllowedCategoryId == null ||
                    x.AllowedCategoryId == product.ProductCategoryId
                ) &&
                (
                    x.CurrentProductVariantId == null ||
                    x.CurrentProductVariantId == request.ProductVariantId
                ))
            .Select(x => new
            {
                x.Id,
                x.SlotCode,
                x.ZoneName,
                MaxCapacity = x.MaxCapacity!.Value,
                x.CurrentOccupancy,
                x.CurrentProductVariantId,
                x.Priority
            })
            .ToListAsync(cancellationToken);

        var ranked = candidates
            .Select(x =>
            {
                var freeCapacity = x.MaxCapacity - x.CurrentOccupancy;
                var capacityFit = Clamp01(request.RequiredWeightKg / freeCapacity);
                var occupancyFit = Clamp01(x.CurrentOccupancy / x.MaxCapacity);
                var isEmpty = x.CurrentProductVariantId == null;

                var categoryMatch = isEmpty
                    ? emptyColumnScore
                    : sameProductScore;

                var priorityNorm = Clamp01(x.Priority / 100m);

                var score = capacityWeight * capacityFit +
                            occupancyWeight * occupancyFit +
                            categoryWeight * categoryMatch +
                            priorityWeight * priorityNorm;

                return new
                {
                    Location = x,
                    IsEmpty = isEmpty,
                    FreeCapacity = freeCapacity,
                    CapacityFit = capacityFit,
                    OccupancyFit = occupancyFit,
                    CategoryMatch = categoryMatch,
                    PriorityNorm = priorityNorm,
                    Score = score
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.IsEmpty) // Cột đang đúng loại trước cột trống
            .ThenBy(x => x.FreeCapacity - request.RequiredWeightKg)
            .ThenByDescending(x => x.Location.Priority)
            .ThenBy(x => x.Location.SlotCode)
            .Take(Math.Clamp(request.Top, 1, 10))
            .Select((x, index) => new PutawaySuggestionDto(
                Rank: index + 1,
                LocationId: x.Location.Id,
                LocationCode: x.Location.SlotCode ?? $"LOC-{x.Location.Id}",
                ZoneName: x.Location.ZoneName,
                CurrentOccupancyKg: x.Location.CurrentOccupancy,
                MaxCapacityKg: x.Location.MaxCapacity,
                FreeCapacityKg: x.FreeCapacity,
                RemainingAfterKg: x.FreeCapacity - request.RequiredWeightKg,
                CurrentProductVariantId: x.Location.CurrentProductVariantId,
                IsEmpty: x.IsEmpty,
                Score: decimal.Round(x.Score, 6),
                ScoreDetails: new PutawayScoreDetailsDto(
                    decimal.Round(x.CapacityFit, 6),
                    decimal.Round(x.OccupancyFit, 6),
                    decimal.Round(x.CategoryMatch, 6),
                    decimal.Round(x.PriorityNorm, 6)),
                Reason: BuildReason(x.IsEmpty, x.CapacityFit, x.OccupancyFit)))
            .ToList();

        if (ranked.Count > 0)
        {
            return ApiResponse.Success(new PutawaySuggestionsResponse(
                HasSuggestion: true,
                WarehouseId: request.WarehouseId,
                ProductVariantId: request.ProductVariantId,
                RequiredWeightKg: request.RequiredWeightKg,
                Suggestions: ranked,
                Message: null));
        }

        // 2. Không có vị trí nào đủ sức chứa toàn bộ -> Kiểm tra khả năng chia lô (Split)
        var splitCandidates = await _context.Locations
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.WarehouseId == request.WarehouseId &&
                x.IsQuarantine == requireQuarantine &&
                x.MaxCapacity != null &&
                x.MaxCapacity > x.CurrentOccupancy &&
                (
                    x.AllowedCategoryId == null ||
                    x.AllowedCategoryId == product.ProductCategoryId
                ) &&
                (
                    x.CurrentProductVariantId == null ||
                    x.CurrentProductVariantId == request.ProductVariantId
                ))
            .Select(x => new
            {
                x.Id,
                x.SlotCode,
                x.ZoneName,
                MaxCapacity = x.MaxCapacity!.Value,
                x.CurrentOccupancy,
                x.CurrentProductVariantId,
                x.Priority
            })
            .ToListAsync(cancellationToken);

        var rankedSplit = splitCandidates
            .Select(x =>
            {
                var freeCapacity = x.MaxCapacity - x.CurrentOccupancy;
                var capacityFit = Clamp01(freeCapacity / request.RequiredWeightKg); // Điểm khít đối với phần chia
                var occupancyFit = Clamp01(x.CurrentOccupancy / x.MaxCapacity);
                var isEmpty = x.CurrentProductVariantId == null;

                var categoryMatch = isEmpty
                    ? emptyColumnScore
                    : sameProductScore;

                var priorityNorm = Clamp01(x.Priority / 100m);

                var score = capacityWeight * capacityFit +
                            occupancyWeight * occupancyFit +
                            categoryWeight * categoryMatch +
                            priorityWeight * priorityNorm;

                return new
                {
                    Location = x,
                    FreeCapacity = freeCapacity,
                    Score = score
                };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var totalFree = rankedSplit.Sum(x => x.FreeCapacity);
        if (totalFree < request.RequiredWeightKg)
        {
            return ApiResponse.Success(new PutawaySuggestionsResponse(
                HasSuggestion: false,
                WarehouseId: request.WarehouseId,
                ProductVariantId: request.ProductVariantId,
                RequiredWeightKg: request.RequiredWeightKg,
                Suggestions: new List<PutawaySuggestionDto>(),
                Message: "Kho đã đầy hoặc không đủ sức chứa trống để phân bổ lô hàng này. Hãy giải phóng bớt không gian hoặc phân chia lô thủ công."));
        }

        // Phân bổ tối ưu (Greedy theo Score cao giảm dần)
        var splitResults = new List<SplitSuggestionDto>();
        var remainingWeight = request.RequiredWeightKg;

        foreach (var item in rankedSplit)
        {
            if (remainingWeight <= 0) break;

            var allocate = Math.Min(item.FreeCapacity, remainingWeight);
            splitResults.Add(new SplitSuggestionDto(
                LocationId: item.Location.Id,
                WeightKg: allocate));

            remainingWeight -= allocate;
        }

        return ApiResponse.Success(new PutawaySuggestionsResponse(
            HasSuggestion: false,
            WarehouseId: request.WarehouseId,
            ProductVariantId: request.ProductVariantId,
            RequiredWeightKg: request.RequiredWeightKg,
            Suggestions: new List<PutawaySuggestionDto>(),
            Message: "Không có ô kệ đơn lẻ nào đủ sức chứa toàn bộ. Đề xuất chia nhỏ lô hàng của bạn như sau:",
            CanSplit: true,
            SplitSuggestions: splitResults));
    }

    public async Task<ApiResponse> GetConfigAsync(int? warehouseId, CancellationToken cancellationToken)
    {
        var capacityWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_CAPACITY_FIT_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_CAPACITY_FIT_WEIGHT,
            warehouseId,
            cancellationToken);

        var occupancyWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_OCCUPANCY_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_OCCUPANCY_WEIGHT,
            warehouseId,
            cancellationToken);

        var categoryWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_CATEGORY_MATCH_WEIGHT,
            warehouseId,
            cancellationToken);

        var priorityWeight = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_PRIORITY_WEIGHT_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_PRIORITY_WEIGHT,
            warehouseId,
            cancellationToken);

        var sameProductScore = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_SAME_PRODUCT_SCORE_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_SAME_PRODUCT_SCORE,
            warehouseId,
            cancellationToken);

        var emptyColumnScore = await ResolveConfigDecimalAsync(
            CommonConstants.SystemConfig.PUTAWAY_EMPTY_COLUMN_SCORE_KEY,
            CommonConstants.SystemConfig.DEFAULT_PUTAWAY_EMPTY_COLUMN_SCORE,
            warehouseId,
            cancellationToken);

        var dto = new PutawayRuleConfigDto
        {
            Id = warehouseId ?? 0,
            WarehouseId = warehouseId,
            CapacityWeight = capacityWeight,
            OccupancyWeight = occupancyWeight,
            CategoryWeight = categoryWeight,
            PriorityWeight = priorityWeight,
            SameProductScore = sameProductScore,
            EmptyColumnScore = emptyColumnScore,
            IsActive = true
        };

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> UpdateConfigAsync(int id, UpdatePutawayRuleConfigDto dto, CancellationToken cancellationToken)
    {
        var total = dto.CapacityWeight + dto.OccupancyWeight + dto.CategoryWeight + dto.PriorityWeight;
        if (Math.Abs(total - 1m) > 0.0001m)
        {
            return ApiResponse.UnprocessableEntity("Tổng bốn trọng số gợi ý vị trí phải bằng 1.", "PUTAWAY_WEIGHT_SUM_INVALID");
        }

        int? warehouseId = id > 0 ? id : null;

        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_CAPACITY_FIT_WEIGHT_KEY, dto.CapacityWeight.ToString("F4"), warehouseId, cancellationToken);
        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_OCCUPANCY_WEIGHT_KEY, dto.OccupancyWeight.ToString("F4"), warehouseId, cancellationToken);
        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY, dto.CategoryWeight.ToString("F4"), warehouseId, cancellationToken);
        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_PRIORITY_WEIGHT_KEY, dto.PriorityWeight.ToString("F4"), warehouseId, cancellationToken);
        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_SAME_PRODUCT_SCORE_KEY, dto.SameProductScore.ToString("F4"), warehouseId, cancellationToken);
        await SaveConfigValueAsync(CommonConstants.SystemConfig.PUTAWAY_EMPTY_COLUMN_SCORE_KEY, dto.EmptyColumnScore.ToString("F4"), warehouseId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(message: "Cập nhật cấu hình quy tắc gợi ý vị trí thành công.");
    }

    public async Task<ApiResponse> ConfirmStoreInAsync(string referenceType, int referenceId, ConfirmStoreInRequest request, CancellationToken cancellationToken)
    {
        if (request.WeightKg <= 0)
        {
            return ApiResponse.BadRequest("Khối lượng thực nhận phải lớn hơn 0.", ApiCodeConstants.Common.BadRequest);
        }

        var isPaddy = referenceType.Equals("PADDY_PURCHASE", StringComparison.OrdinalIgnoreCase);

        // 1. Kiểm soát tranh chấp (Lock/Transaction)
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 2. Kiểm tra tài liệu nguồn (Source Document) + M1: cho phép split
            if (isPaddy)
            {
                var paddyReceipt = await _context.PaddyPurchaseReceipts
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);

                if (paddyReceipt == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu thu mua lúa gốc.", ApiCodeConstants.Common.NotFound);

                // M1: Cho phép split — chỉ block khi đã nhập đủ hoặc vượt quá tổng khối lượng
                var totalStoredKg = await _context.PutawayDecisions
                    .Where(x => x.ReferenceType == "PADDY_PURCHASE" && x.ReferenceId == referenceId)
                    .SumAsync(x => x.RequiredWeightKg, cancellationToken);

                if (totalStoredKg >= paddyReceipt.ActualWeightKg)
                    return ApiResponse.Conflict(
                        $"Phiếu thu mua lúa này đã nhập kho đủ {totalStoredKg:N0} kg (tổng: {paddyReceipt.ActualWeightKg:N0} kg).",
                        "DOCUMENT_ALREADY_STORED");
            }
            else
            {
                var inboundOrder = await _context.InboundOrders
                    .Include(x => x.InboundOrderItems)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);

                if (inboundOrder == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu nhập kho nguồn.", ApiCodeConstants.Common.NotFound);

                // M1: Cho phép split — chỉ block khi tổng đã store-in >= tổng số lượng đặt
                var totalOrderedQty = inboundOrder.InboundOrderItems
                    .Where(i => !i.IsDeleted)
                    .Sum(i => i.QuantityOrdered);

                var totalStoredKg = await _context.PutawayDecisions
                    .Where(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId)
                    .SumAsync(x => x.RequiredWeightKg, cancellationToken);

                if (totalStoredKg >= totalOrderedQty && totalOrderedQty > 0)
                    return ApiResponse.Conflict(
                        $"Phiếu nhập kho này đã nhập kho đủ {totalStoredKg:N0} kg (tổng: {totalOrderedQty:N0} kg).",
                        "DOCUMENT_ALREADY_STORED");
            }

            // 3. Khách hàng xếp vị trí khác vị trí đề xuất (Override Check)
            var isOverride = request.SuggestedLocationId.HasValue && request.SuggestedLocationId != request.SelectedLocationId;
            if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                return ApiResponse.UnprocessableEntity("Vui lòng nhập lý do thay đổi vị trí so với đề xuất của hệ thống.", "OVERRIDE_REASON_REQUIRED");
            }

            // 4. Tìm vị trí kệ
            var location = await _context.Locations
                .FirstOrDefaultAsync(x => x.Id == request.SelectedLocationId && !x.IsDeleted && x.IsActive, cancellationToken);

            if (location == null)
                return ApiResponse.NotFound("Vị trí kệ không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.NotFound);

            // 5. Cập nhật sức chứa ô kệ an toàn (Concurrency checks)
            var rowsAffected = await _locationRepository.UpdateCapacitySafetyAsync(
                request.SelectedLocationId,
                location.WarehouseId,
                request.WeightKg,
                request.ProductVariantId,
                location.IsQuarantine,
                userId: GetCurrentUserId()
            );

            if (rowsAffected <= 0)
            {
                return ApiResponse.Conflict("Tình trạng vị trí đã thay đổi hoặc không đủ sức chứa thực tế. Vui lòng tải lại trang gợi ý.", "CONCURRENCY_CAPACITY_CLASH");
            }

            // 6. Ghi nhận tồn kho vật lý (Inventory)
            var now = DateTimeHelper.VietnamNow();
            var userId = GetCurrentUserId();

            var inv = await _context.Inventories
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == location.WarehouseId &&
                    x.LocationId == request.SelectedLocationId &&
                    x.ProductVariantId == request.ProductVariantId &&
                    x.PaddyLotId == request.PaddyLotId,
                    cancellationToken);

            if (inv == null)
            {
                inv = new Inventory
                {
                    WarehouseId = location.WarehouseId,
                    LocationId = request.SelectedLocationId,
                    ProductVariantId = request.ProductVariantId,
                    PaddyLotId = request.PaddyLotId,
                    CostPrice = 0,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                _context.Inventories.Add(inv);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var beforeQty = inv.QuantityOnHand;
            inv.QuantityOnHand += request.WeightKg;
            inv.LastModifiedDate = now;
            inv.UpdatedBy = userId;

            _context.Inventories.Update(inv);

            // 7. Tạo InventoryTransaction (Nhật ký giao dịch kho)
            var txn = new InventoryTransaction
            {
                InventoryId = inv.Id,
                WarehouseId = location.WarehouseId,
                LocationId = request.SelectedLocationId,
                ProductVariantId = request.ProductVariantId,
                PaddyLotId = request.PaddyLotId,
                TransactionType = InventoryTransactionTypeConstants.Import,
                ReferenceType = isPaddy ? InventoryReferenceTypeConstants.PaddyPurchase : InventoryReferenceTypeConstants.InboundOrder,
                ReferenceId = referenceId,
                Quantity = request.WeightKg,
                BeforeQuantity = beforeQty,
                AfterQuantity = inv.QuantityOnHand,
                WeightKg = request.WeightKg,
                Note = isOverride ? $"Nhập kho ghi đè: {request.OverrideReason}" : "Nhập kho theo đề xuất Smart Put-away",
                CreatedBy = userId,
                CreatedDate = now
            };
            _context.InventoryTransactions.Add(txn);

            // 8. Lưu PutawayDecision
            var decision = new PutawayDecision
            {
                WarehouseId = location.WarehouseId,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ProductVariantId = request.ProductVariantId,
                PaddyLotId = request.PaddyLotId,
                SuggestedLocationId = request.SuggestedLocationId,
                SelectedLocationId = request.SelectedLocationId,
                IsOverride = isOverride,
                OverrideReason = request.OverrideReason,
                RequiredWeightKg = request.WeightKg,
                CreatedBy = userId,
                CreatedDate = now
            };
            _context.PutawayDecisions.Add(decision);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            try
            {
                var targetWarehouseId = location.WarehouseId;
                var targetProductVariantId = request.ProductVariantId;
                _scheduledJobService.Enqueue<ILowStockDetectionService>(s => s.DetectLowStockForProductAsync(targetWarehouseId, targetProductVariantId, CancellationToken.None));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi enqueue LowStockDetection job sau khi nhập kho thành công.");
            }

            return ApiResponse.Success(message: "Xác nhận nhập kho thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            var safeReferenceType = (referenceType ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogError(ex, "ConfirmStoreInAsync failed. RefType: {RefType}, RefId: {RefId}", safeReferenceType, referenceId);
            return ApiResponse.BadRequest($"Lỗi xác nhận nhập kho: {ex.Message}", ApiCodeConstants.Common.BadRequest);
        }
    }

    private static decimal Clamp01(decimal val) => Math.Clamp(val, 0m, 1m);

    private static string BuildReason(bool isEmpty, decimal capacityFit, decimal occupancyFit)
    {
        if (isEmpty)
        {
            if (capacityFit >= 0.9m) return "Kệ trống, sức chứa lý tưởng cho lô hàng này.";
            return "Kệ trống, phù hợp xếp kho nhanh.";
        }
        if (occupancyFit >= 0.8m) return "Kệ chứa đúng sản phẩm, sắp lấp đầy tối đa diện tích sàn.";
        return "Gom hàng cùng loại sản phẩm để tối ưu không gian phân vùng.";
    }
}

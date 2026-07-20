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
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class PutawaySuggestionService : IPutawaySuggestionService
{
    private readonly IApplicationDbContext _context;
    private readonly ILocationRepository _locationRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PutawaySuggestionService> _logger;

    public PutawaySuggestionService(
        IApplicationDbContext context,
        ILocationRepository locationRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PutawaySuggestionService> logger)
    {
        _context = context;
        _locationRepository = locationRepository;
        _httpContextAccessor = httpContextAccessor;
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

        // Validate: Khối lượng yêu cầu gợi ý cất kho không được vượt quá số lượng hàng thực tế còn lại ở vị trí đệm (LocationId = null)
        if (request.PaddyLotId.HasValue)
        {
            var bufferInv = await _context.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == request.WarehouseId &&
                    x.LocationId == null &&
                    x.ProductVariantId == request.ProductVariantId &&
                    x.PaddyLotId == request.PaddyLotId,
                    cancellationToken);

            var bufferQty = bufferInv?.QuantityOnHand ?? 0m;
            if (request.RequiredWeightKg > bufferQty)
            {
                return ApiResponse.BadRequest(
                    $"Khối lượng yêu cầu gợi ý cất kho ({request.RequiredWeightKg:N0} kg) vượt quá khối lượng còn lại trong khu vực đệm ({bufferQty:N0} kg) của lô hàng này.",
                    "PUTAWAY_WEIGHT_EXCEEDS_BUFFER");
            }
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
                Message: $"Kho không đủ sức chứa trống để phân bổ lô hàng {request.RequiredWeightKg:N0} kg này. Tổng sức chứa trống còn lại của tất cả ô kệ cộng lại chỉ là {totalFree:N0} kg. Vui lòng giải phóng bớt không gian hoặc chia nhỏ lô hàng nhỏ hơn mức này.",
                CanSplit: false,
                TotalFreeCapacityKg: totalFree,
                SplitSuggestions: null));
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
            Message: $"Không có ô kệ đơn lẻ nào đủ sức chứa toàn bộ {request.RequiredWeightKg:N0} kg. Đề xuất chia nhỏ lô hàng của bạn vào các ô kệ như sau (Tổng sức chứa trống khả dụng trong kho: {totalFree:N0} kg):",
            CanSplit: true,
            TotalFreeCapacityKg: totalFree,
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

        // BLOCKING-2 guard: Luồng lúa (PADDY_PURCHASE) bắt buộc phải có PaddyLotId để đảm bảo
        // tồn kho đệm (LocationId=null) được tìm đúng và trừ đúng.
        if (isPaddy && !request.PaddyLotId.HasValue)
        {
            return ApiResponse.BadRequest(
                "Luồng thu mua lúa (PADDY_PURCHASE) bắt buộc phải truyền PaddyLotId để định danh lô hàng và trừ tồn kho đệm chính xác.",
                "PADDY_LOT_ID_REQUIRED");
        }

        // 1. Kiểm soát tranh chấp (Lock/Transaction)
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Bỏ qua nếu database provider không hỗ trợ transaction (ví dụ InMemory)
        }
        try
        {
            // 2. Tìm vị trí kệ trước tiên để kiểm tra kho hàng
            var location = await _context.Locations
                .FirstOrDefaultAsync(x => x.Id == request.SelectedLocationId && !x.IsDeleted && x.IsActive, cancellationToken);

            if (location == null)
                return ApiResponse.NotFound("Vị trí kệ không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.NotFound);

            // 3. Kiểm tra tài liệu nguồn (Source Document) + M1: cho phép split
            PaddyPurchaseReceipt paddyReceipt = null;
            InboundOrder inboundOrder = null;
            Backend.Domain.Entities.PaddyLot lot = null;

            if (isPaddy)
            {
                paddyReceipt = await _context.PaddyPurchaseReceipts
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);

                if (paddyReceipt == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu thu mua lúa gốc.", ApiCodeConstants.Common.NotFound);

                lot = await _context.PaddyLots
                    .FirstOrDefaultAsync(x => x.Id == request.PaddyLotId!.Value && !x.IsDeleted, cancellationToken);

                if (lot == null)
                    return ApiResponse.Conflict("Lô hàng không tồn tại.", "LOT_NOT_FOUND");

                if (lot.SourceReceiptId != referenceId)
                    return ApiResponse.Conflict("Lô hàng không thuộc về phiếu thu mua này.", "LOT_NOT_MATCH_RECEIPT");

                if (lot.ProductVariantId != request.ProductVariantId)
                    return ApiResponse.Conflict("Thông tin biến thể sản phẩm của lô không khớp.", "LOT_VARIANT_MISMATCH");

                if (lot.WarehouseId != location.WarehouseId)
                    return ApiResponse.Conflict("Vị trí được chọn không thuộc kho hàng của lô lúa.", "WAREHOUSE_MISMATCH");

                // M1: Cho phép split — chỉ block khi đã nhập đủ hoặc vượt quá tổng khối lượng
                var totalStoredKg = await _context.PutawayDecisions
                    .Where(x => x.ReferenceType == "PADDY_PURCHASE" && x.ReferenceId == referenceId)
                    .SumAsync(x => x.RequiredWeightKg, cancellationToken);

                if (totalStoredKg + request.WeightKg > paddyReceipt.ActualWeightKg)
                {
                    var remaining = Math.Max(0m, paddyReceipt.ActualWeightKg - totalStoredKg);
                    return ApiResponse.Conflict(
                        $"Khối lượng cất kho yêu cầu ({request.WeightKg:N0} kg) vượt quá khối lượng còn lại chưa cất của phiếu thu mua này ({remaining:N0} kg / tổng: {paddyReceipt.ActualWeightKg:N0} kg).",
                        "STORE_IN_WEIGHT_EXCEEDS_REMAINING");
                }
            }
            else
            {
                inboundOrder = await _context.InboundOrders
                    .Include(x => x.InboundOrderItems)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);

                if (inboundOrder == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu nhập kho nguồn.", ApiCodeConstants.Common.NotFound);

                if (inboundOrder.WarehouseId != location.WarehouseId)
                    return ApiResponse.Conflict("Vị trí được chọn không thuộc kho hàng của phiếu nhập.", "WAREHOUSE_MISMATCH");

                // M1: Cho phép split — chỉ block khi tổng đã store-in >= tổng số lượng đặt
                var totalOrderedQty = inboundOrder.InboundOrderItems
                    .Where(i => !i.IsDeleted)
                    .Sum(i => i.QuantityOrdered);

                var totalStoredKg = await _context.PutawayDecisions
                    .Where(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId)
                    .SumAsync(x => x.RequiredWeightKg, cancellationToken);

                if (totalStoredKg + request.WeightKg > totalOrderedQty && totalOrderedQty > 0)
                {
                    var remaining = Math.Max(0m, totalOrderedQty - totalStoredKg);
                    return ApiResponse.Conflict(
                        $"Khối lượng cất kho yêu cầu ({request.WeightKg:N0} kg) vượt quá khối lượng còn lại chưa cất của phiếu nhập kho này ({remaining:N0} kg / tổng: {totalOrderedQty:N0} kg).",
                        "STORE_IN_WEIGHT_EXCEEDS_REMAINING");
                }
            }

            // 4. Khách hàng xếp vị trí khác vị trí đề xuất (Override Check)
            var isOverride = request.SuggestedLocationId.HasValue && request.SuggestedLocationId != request.SelectedLocationId;
            if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                return ApiResponse.UnprocessableEntity("Vui lòng nhập lý do thay đổi vị trí so với đề xuất của hệ thống.", "OVERRIDE_REASON_REQUIRED");
            }

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

            decimal sourceCostPrice = 0m;

            // RC-3 & Putaway fix: Đối với lúa (isPaddy), do lúc chốt phiếu đã tạm ghi nhận tồn tại khu đệm (LocationId = null),
            // nên khi xếp vào ô kệ thực tế ta bắt buộc phải TRỪ số lượng tương ứng ở khu đệm này.
            if (isPaddy)
            {
                var bufferInv = await _context.Inventories
                    .FirstOrDefaultAsync(x =>
                        !x.IsDeleted &&
                        x.WarehouseId == location.WarehouseId &&
                        x.LocationId == null &&
                        x.ProductVariantId == request.ProductVariantId &&
                        x.PaddyLotId == request.PaddyLotId,
                        cancellationToken);

                if (bufferInv == null)
                {
                    return ApiResponse.Conflict("Không tìm thấy tồn kho đệm cho lô lúa này.", "BUFFER_INVENTORY_NOT_FOUND");
                }

                if (bufferInv.QuantityOnHand < request.WeightKg)
                {
                    return ApiResponse.Conflict(
                        $"Số lượng tồn trong khu đệm ({bufferInv.QuantityOnHand:N0} kg) không đủ để chuyển đi ({request.WeightKg:N0} kg).",
                        "INSUFFICIENT_BUFFER_STOCK");
                }

                // Bảo toàn giá vốn: Lấy từ khu đệm, fallback là PaddyLot.CostPricePerKg
                sourceCostPrice = bufferInv.CostPrice > 0 ? bufferInv.CostPrice : (lot?.CostPricePerKg ?? 0m);

                var bufferBeforeQty = bufferInv.QuantityOnHand;
                bufferInv.QuantityOnHand -= request.WeightKg; // Trừ trực tiếp, không dùng Clamp01 vì đã check đủ
                bufferInv.LastModifiedDate = now;
                bufferInv.UpdatedBy = userId;
                _context.Inventories.Update(bufferInv);

                // Tạo lịch sử giao dịch xuất lúa từ khu vực đệm
                var bufferTxn = new InventoryTransaction
                {
                    InventoryId = bufferInv.Id,
                    WarehouseId = location.WarehouseId,
                    LocationId = null,
                    ProductVariantId = request.ProductVariantId,
                    PaddyLotId = request.PaddyLotId,
                    TransactionType = InventoryTransactionTypeConstants.Export,
                    ReferenceType = InventoryReferenceTypeConstants.PaddyPurchase,
                    ReferenceId = referenceId,
                    Quantity = request.WeightKg,
                    BeforeQuantity = bufferBeforeQty,
                    AfterQuantity = bufferInv.QuantityOnHand,
                    WeightKg = request.WeightKg,
                    Note = "Xuất từ khu vực đệm trung chuyển sang ô kệ thực tế",
                    CreatedBy = userId,
                    CreatedDate = now
                };
                _context.InventoryTransactions.Add(bufferTxn);
            }

            var inv = await _context.Inventories
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == location.WarehouseId &&
                    x.LocationId == request.SelectedLocationId &&
                    x.ProductVariantId == request.ProductVariantId &&
                    x.PaddyLotId == request.PaddyLotId,
                    cancellationToken);

            var beforeQty = inv != null ? inv.QuantityOnHand : 0m;

            if (inv == null)
            {
                inv = new Inventory
                {
                    WarehouseId = location.WarehouseId,
                    LocationId = request.SelectedLocationId,
                    ProductVariantId = request.ProductVariantId,
                    PaddyLotId = request.PaddyLotId,
                    CostPrice = isPaddy ? sourceCostPrice : 0m,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                _context.Inventories.Add(inv);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Nếu đã có tồn cùng lô thì tính giá vốn bình quân gia quyền đúng
                if (isPaddy)
                {
                    if (beforeQty > 0 && inv.CostPrice > 0)
                    {
                        var newCost = ((beforeQty * inv.CostPrice) + (request.WeightKg * sourceCostPrice)) / (beforeQty + request.WeightKg);
                        inv.CostPrice = Math.Round(newCost, 2);
                    }
                    else
                    {
                        inv.CostPrice = sourceCostPrice;
                    }
                }
            }

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

            // R5-1: Lưu trước các thay đổi để SumAsync ở bước 9 có thể query thấy bản ghi hiện tại trên database thật
            await _context.SaveChangesAsync(cancellationToken);

            // 9. Cập nhật trạng thái lịch hẹn khi và chỉ khi store-in thành công (PADDY_PURCHASE)
            if (isPaddy && paddyReceipt.ScheduleId.HasValue)
            {
                var scheduleId = paddyReceipt.ScheduleId.Value;
                var schedule = await _context.PaddyPurchaseSchedules
                    .FirstOrDefaultAsync(x => x.Id == scheduleId && !x.IsDeleted, cancellationToken);

                if (schedule != null)
                {
                    var scheduleStatuses = await _context.PaddyPurchaseScheduleStatuses
                        .Where(x => !x.IsDeleted)
                        .ToListAsync(cancellationToken);

                    var cancelledStatusId = scheduleStatuses.FirstOrDefault(x => x.Code == "CANCELLED")?.Id ?? 6;
                    var stockedStatusId = scheduleStatuses.FirstOrDefault(x => x.Code == "STOCKED")?.Id ?? 5;
                    var partiallyStockedStatusId = scheduleStatuses.FirstOrDefault(x => x.Code == "PARTIALLY_STOCKED")?.Id ?? 7;
                    var weighedStatusId = scheduleStatuses.FirstOrDefault(x => x.Code == "WEIGHED")?.Id ?? 4;

                    if (schedule.StatusId != cancelledStatusId)
                    {
                        var scheduleReceipts = await _context.PaddyPurchaseReceipts
                            .Where(x => x.ScheduleId == scheduleId && !x.IsDeleted)
                            .ToListAsync(cancellationToken);

                        var receiptWeights = new List<(decimal StoredWeightKg, decimal ActualWeightKg)>();
                        foreach (var r in scheduleReceipts)
                        {
                            var totalStoredWeight = await _context.PutawayDecisions
                                .Where(x => x.ReferenceType == "PADDY_PURCHASE" && x.ReferenceId == r.Id)
                                .SumAsync(x => x.RequiredWeightKg, cancellationToken);

                            receiptWeights.Add((totalStoredWeight, r.ActualWeightKg));
                        }

                        var statusCode = PaddyScheduleStatusHelper.DetermineScheduleStatus(receiptWeights);
                        var targetStatusId = scheduleStatuses.FirstOrDefault(x => x.Code == statusCode)?.Id
                            ?? (statusCode == "WEIGHED" ? weighedStatusId : (statusCode == "STOCKED" ? stockedStatusId : partiallyStockedStatusId));

                        if (schedule.StatusId != targetStatusId)
                        {
                            schedule.StatusId = targetStatusId;
                            schedule.UpdatedBy = userId;
                            schedule.LastModifiedDate = now;
                            _context.PaddyPurchaseSchedules.Update(schedule);
                        }
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
                transaction.Dispose();
            }

            return ApiResponse.Success(message: "Xác nhận nhập kho thành công.");
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
                transaction.Dispose();
            }
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

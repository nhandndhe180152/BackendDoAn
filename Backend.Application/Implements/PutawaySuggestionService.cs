using System;
using System.Collections.Generic;
using System.Linq;
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

        var config = await _context.PutawayRuleConfigs
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                (x.WarehouseId == request.WarehouseId ||
                 x.WarehouseId == null))
            .OrderByDescending(x => x.WarehouseId.HasValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            return ApiResponse.BadRequest("Chưa cấu hình quy tắc gợi ý vị trí.", ApiCodeConstants.Common.BadRequest);
        }

        var totalWeight = config.CapacityWeight + config.OccupancyWeight + config.CategoryWeight + config.PriorityWeight;
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
                    ? config.EmptyColumnScore
                    : config.SameProductScore;

                var priorityNorm = Clamp01(x.Priority / 100m);

                var score = config.CapacityWeight * capacityFit +
                            config.OccupancyWeight * occupancyFit +
                            config.CategoryWeight * categoryMatch +
                            config.PriorityWeight * priorityNorm;

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
                FreeCapacity = x.MaxCapacity!.Value - x.CurrentOccupancy
            })
            .OrderByDescending(x => x.FreeCapacity)
            .ToListAsync(cancellationToken);

        var totalFreeCapacity = splitCandidates.Sum(x => x.FreeCapacity);
        if (totalFreeCapacity >= request.RequiredWeightKg)
        {
            var remainingToAllocate = request.RequiredWeightKg;
            var splits = new List<SplitSuggestionDto>();
            foreach (var sc in splitCandidates)
            {
                if (remainingToAllocate <= 0) break;
                var allocated = Math.Min(sc.FreeCapacity, remainingToAllocate);
                splits.Add(new SplitSuggestionDto(sc.Id, allocated));
                remainingToAllocate -= allocated;
            }

            return ApiResponse.Success(new PutawaySuggestionsResponse(
                HasSuggestion: false,
                WarehouseId: request.WarehouseId,
                ProductVariantId: request.ProductVariantId,
                RequiredWeightKg: request.RequiredWeightKg,
                Suggestions: new List<PutawaySuggestionDto>(),
                Message: "Không có vị trí đơn lẻ nào đủ sức chứa toàn bộ. Đã đề xuất phương án chia nhỏ lô.",
                CanSplit: true,
                TotalFreeCapacityKg: totalFreeCapacity,
                SplitSuggestions: splits));
        }

        return ApiResponse.Success(new PutawaySuggestionsResponse(
            HasSuggestion: false,
            WarehouseId: request.WarehouseId,
            ProductVariantId: request.ProductVariantId,
            RequiredWeightKg: request.RequiredWeightKg,
            Suggestions: new List<PutawaySuggestionDto>(),
            Message: "Không có vị trí nào đủ sức chứa. Hãy chia lô hoặc cập nhật sức chứa.",
            CanSplit: false));
    }

    public async Task<ApiResponse> GetConfigAsync(int? warehouseId, CancellationToken cancellationToken)
    {
        var config = await _context.PutawayRuleConfigs
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && (x.WarehouseId == warehouseId || x.WarehouseId == null))
            .OrderByDescending(x => x.WarehouseId.HasValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
            return ApiResponse.NotFound("Không tìm thấy cấu hình quy tắc.", ApiCodeConstants.Common.NotFound);

        var dto = new PutawayRuleConfigDto
        {
            Id = config.Id,
            WarehouseId = config.WarehouseId,
            CapacityWeight = config.CapacityWeight,
            OccupancyWeight = config.OccupancyWeight,
            CategoryWeight = config.CategoryWeight,
            PriorityWeight = config.PriorityWeight,
            SameProductScore = config.SameProductScore,
            EmptyColumnScore = config.EmptyColumnScore,
            IsActive = config.IsActive
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

        var config = await _context.PutawayRuleConfigs
            .Where(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
            return ApiResponse.NotFound("Không tìm thấy cấu hình quy tắc.", ApiCodeConstants.Common.NotFound);

        var now = DateTimeHelper.VietnamNow();
        config.CapacityWeight = dto.CapacityWeight;
        config.OccupancyWeight = dto.OccupancyWeight;
        config.CategoryWeight = dto.CategoryWeight;
        config.PriorityWeight = dto.PriorityWeight;
        config.SameProductScore = dto.SameProductScore;
        config.EmptyColumnScore = dto.EmptyColumnScore;
        config.LastModifiedDate = now;

        _context.PutawayRuleConfigs.Update(config);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(message: "Cập nhật cấu hình quy tắc gợi ý vị trí thành công.");
    }

    public async Task<ApiResponse> ConfirmStoreInAsync(string referenceType, int referenceId, ConfirmStoreInRequest request, CancellationToken cancellationToken)
    {
        var refTypeUpper = referenceType.Trim().ToUpper();

        // 1. Kiểm tra document nguồn hợp lệ & chưa confirm
        int warehouseId = 0;
        int? paddyLotId = request.PaddyLotId;

        switch (refTypeUpper)
        {
            case "PADDY_PURCHASE":
                var receipt = await _context.PaddyPurchaseReceipts
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);
                if (receipt == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu mua lúa.", ApiCodeConstants.Common.NotFound);

                // Check đã confirm qua PutawayDecision chưa
                var alreadyConfirmed = await _context.PutawayDecisions
                    .AnyAsync(x => x.ReferenceType == "PADDY_PURCHASE" && x.ReferenceId == referenceId && !x.IsDeleted, cancellationToken);
                if (alreadyConfirmed)
                    return ApiResponse.Conflict("Phiếu mua lúa đã được nhập kho trước đó.", ApiCodeConstants.Common.DuplicatedData);

                warehouseId = receipt.WarehouseId;
                break;

            case "PURCHASE_ORDER":
                var inbound = await _context.InboundOrders
                    .Include(x => x.InboundOrderStatus)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);
                if (inbound == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu nhập kho.", ApiCodeConstants.Common.NotFound);

                if (inbound.InboundOrderStatus?.Name == InboundOrderStatusNames.Confirmed)
                    return ApiResponse.Conflict("Phiếu nhập kho đã hoàn tất nhập kho trước đó.", ApiCodeConstants.Common.DuplicatedData);

                warehouseId = inbound.WarehouseId;
                break;

            case "MILLING_OUTPUT":
                var milling = await _context.MillingOrders
                    .Include(x => x.Status)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);
                if (milling == null)
                    return ApiResponse.NotFound("Không tìm thấy lệnh xay.", ApiCodeConstants.Common.NotFound);

                if (milling.Status?.Name == "Hoàn tất")
                    return ApiResponse.Conflict("Lệnh xay đã hoàn tất nhập kho trước đó.", ApiCodeConstants.Common.DuplicatedData);

                warehouseId = milling.WarehouseId;
                break;

            case "CUSTOMER_RETURN":
                var retOrder = await _context.CustomerReturnOrders
                    .Include(x => x.CustomerReturnOrderStatus)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);
                if (retOrder == null)
                    return ApiResponse.NotFound("Không tìm thấy đơn hoàn trả.", ApiCodeConstants.Common.NotFound);

                if (retOrder.CustomerReturnOrderStatus?.Name == "Hoàn tất")
                    return ApiResponse.Conflict("Đơn hoàn trả đã hoàn tất trước đó.", ApiCodeConstants.Common.DuplicatedData);

                warehouseId = retOrder.WarehouseId;
                break;

            case "STOCK_TRANSFER":
                var transfer = await _context.StockTransfers
                    .Include(x => x.Status)
                    .FirstOrDefaultAsync(x => x.Id == referenceId && !x.IsDeleted, cancellationToken);
                if (transfer == null)
                    return ApiResponse.NotFound("Không tìm thấy phiếu chuyển kho.", ApiCodeConstants.Common.NotFound);

                if (transfer.Status?.Name == "Hoàn tất")
                    return ApiResponse.Conflict("Phiếu chuyển kho đã hoàn tất trước đó.", ApiCodeConstants.Common.DuplicatedData);

                warehouseId = transfer.ToWarehouseId;
                break;

            default:
                return ApiResponse.BadRequest($"Loại chứng từ nguồn '{referenceType}' không được hỗ trợ.", ApiCodeConstants.Common.BadRequest);
        }

        // 2. Kiểm tra ProductVariant & PaddyLot
        var pv = await _context.ProductVariants
            .FirstOrDefaultAsync(x => x.Id == request.ProductVariantId && !x.IsDeleted, cancellationToken);
        if (pv == null)
            return ApiResponse.NotFound("Không tìm thấy loại sản phẩm.", ApiCodeConstants.Common.NotFound);

        if (paddyLotId.HasValue)
        {
            var lot = await _context.PaddyLots
                .FirstOrDefaultAsync(x => x.Id == paddyLotId.Value && !x.IsDeleted, cancellationToken);
            if (lot == null)
                return ApiResponse.NotFound("Không tìm thấy lô hàng.", ApiCodeConstants.Common.NotFound);
        }

        // 3. Kiểm tra lý do override (nếu chọn khác gợi ý hạng 1)
        var selectedLocation = await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == request.SelectedLocationId && !x.IsDeleted, cancellationToken);
        if (selectedLocation == null)
            return ApiResponse.NotFound("Không tìm thấy vị trí lưu kho đã chọn.", ApiCodeConstants.Common.NotFound);

        var isOverride = false;
        decimal? suggestedScore = null;

        if (request.SuggestedLocationId.HasValue && request.SuggestedLocationId.Value != request.SelectedLocationId)
        {
            isOverride = true;
            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                return ApiResponse.UnprocessableEntity("Vui lòng nhập lý do khi chọn vị trí khác đề xuất.", "PUTAWAY_OVERRIDE_REASON_REQUIRED");
            }
        }

        // Bọc trong transaction nguyên tử
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Set HttpContext bypass flag to prevent EF change tracker from double-incrementing Location occupancy
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items["BypassLocationOccupancyInterceptor"] = true;
            }

            var userId = GetCurrentUserId();

            // 4. Atomic UPDATE Location (MySQL lock-free updates)
            var affectedRows = await _locationRepository.UpdateCapacitySafetyAsync(
                request.SelectedLocationId,
                warehouseId,
                request.WeightKg,
                request.ProductVariantId,
                selectedLocation.IsQuarantine,
                userId
            );

            if (affectedRows == 0)
            {
                return ApiResponse.Conflict("Tình trạng vị trí đã thay đổi hoặc không đủ sức chứa. Vui lòng lấy lại gợi ý.", ApiCodeConstants.Common.DuplicatedData);
            }

            // 5. Tìm hoặc tạo Inventory
            var inv = await _context.Inventories
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == warehouseId &&
                    x.LocationId == request.SelectedLocationId &&
                    x.ProductVariantId == request.ProductVariantId &&
                    x.PaddyLotId == paddyLotId,
                    cancellationToken);

            var now = DateTimeHelper.VietnamNow();
            decimal beforeQty = 0;

            if (inv == null)
            {
                inv = new Inventory
                {
                    WarehouseId = warehouseId,
                    LocationId = request.SelectedLocationId,
                    ProductVariantId = request.ProductVariantId,
                    PaddyLotId = paddyLotId,
                    CostPrice = pv.CostPrice,
                    QuantityOnHand = request.WeightKg,
                    QuantityReserved = 0,
                    CreatedDate = now,
                    CreatedBy = userId
                };
                _context.Inventories.Add(inv);
            }
            else
            {
                beforeQty = inv.QuantityOnHand;
                inv.QuantityOnHand += request.WeightKg;
                inv.LastModifiedDate = now;
                inv.UpdatedBy = userId;
                _context.Inventories.Update(inv);
            }
            await _context.SaveChangesAsync(cancellationToken);

            // 6. Ghi InventoryTransaction (IMPORT)
            var txn = new InventoryTransaction
            {
                InventoryId = inv.Id,
                WarehouseId = warehouseId,
                LocationId = request.SelectedLocationId,
                ProductVariantId = request.ProductVariantId,
                PaddyLotId = paddyLotId,
                TransactionType = "IMPORT",
                ReferenceType = refTypeUpper,
                ReferenceId = referenceId,
                Quantity = request.WeightKg,
                BeforeQuantity = beforeQty,
                AfterQuantity = inv.QuantityOnHand,
                WeightKg = request.WeightKg,
                Note = request.OverrideReason ?? $"Xác nhận Put-away lưu kho từ chứng từ {refTypeUpper} #{referenceId}",
                CreatedDate = now,
                CreatedBy = userId
            };
            _context.InventoryTransactions.Add(txn);

            // 7. Cập nhật PaddyLot.RemainingWeightKg (nếu có)
            if (paddyLotId.HasValue)
            {
                var lot = await _context.PaddyLots
                    .FirstOrDefaultAsync(x => x.Id == paddyLotId.Value, cancellationToken);
                if (lot != null)
                {
                    lot.RemainingWeightKg += request.WeightKg;
                    lot.LastModifiedDate = now;
                    lot.UpdatedBy = userId;
                    _context.PaddyLots.Update(lot);
                }
            }

            // 8. Lưu PutawayDecision
            var decision = new PutawayDecision
            {
                WarehouseId = warehouseId,
                ProductVariantId = request.ProductVariantId,
                PaddyLotId = paddyLotId,
                ReferenceType = refTypeUpper,
                ReferenceId = referenceId,
                RequiredWeightKg = request.WeightKg,
                SuggestedLocationId = request.SuggestedLocationId,
                SelectedLocationId = request.SelectedLocationId,
                SuggestedScore = suggestedScore,
                IsOverride = isOverride,
                OverrideReason = request.OverrideReason,
                CreatedBy = userId,
                CreatedDate = now
            };
            _context.PutawayDecisions.Add(decision);

            // 9. Cập nhật trạng thái chứng từ thành Confirmed
            switch (refTypeUpper)
            {
                case "PURCHASE_ORDER":
                    var inbound = await _context.InboundOrders
                        .FirstOrDefaultAsync(x => x.Id == referenceId, cancellationToken);
                    if (inbound != null)
                    {
                        var status = await _context.InboundOrderStatuses
                            .FirstOrDefaultAsync(x => x.Name == InboundOrderStatusNames.Confirmed && !x.IsDeleted, cancellationToken);
                        if (status != null)
                        {
                            inbound.InboundOrderStatusId = status.Id;
                            inbound.CompletedDate = now;
                            inbound.LastModifiedDate = now;
                            inbound.UpdatedBy = userId;
                            _context.InboundOrders.Update(inbound);
                        }
                    }
                    break;

                case "MILLING_OUTPUT":
                    var milling = await _context.MillingOrders
                        .FirstOrDefaultAsync(x => x.Id == referenceId, cancellationToken);
                    if (milling != null)
                    {
                        milling.StatusId = 5; // Hoàn tất
                        milling.CompletedAt = now;
                        milling.LastModifiedDate = now;
                        milling.UpdatedBy = userId;
                        _context.MillingOrders.Update(milling);
                    }
                    break;

                case "CUSTOMER_RETURN":
                    var retOrder = await _context.CustomerReturnOrders
                        .FirstOrDefaultAsync(x => x.Id == referenceId, cancellationToken);
                    if (retOrder != null)
                    {
                        var status = await _context.CustomerReturnOrderStatuses
                            .FirstOrDefaultAsync(x => x.Name == "Hoàn tất" && !x.IsDeleted, cancellationToken);
                        if (status != null)
                        {
                            retOrder.CustomerReturnOrderStatusId = status.Id;
                            retOrder.CompletedDate = now;
                            retOrder.LastModifiedDate = now;
                            retOrder.UpdatedBy = userId;
                            _context.CustomerReturnOrders.Update(retOrder);
                        }
                    }
                    break;

                case "STOCK_TRANSFER":
                    var transfer = await _context.StockTransfers
                        .FirstOrDefaultAsync(x => x.Id == referenceId, cancellationToken);
                    if (transfer != null)
                    {
                        transfer.StatusId = 3; // Hoàn tất
                        transfer.TransferDate = now;
                        transfer.LastModifiedDate = now;
                        transfer.UpdatedBy = userId;
                        _context.StockTransfers.Update(transfer);
                    }
                    break;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Clean bypass flag from HttpContext
            if (httpContext != null)
            {
                httpContext.Items.Remove("BypassLocationOccupancyInterceptor");
            }

            return ApiResponse.Success(message: "Xác nhận đưa hàng vào vị trí lưu kho thành công.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xác nhận put-away store-in.");
            throw;
        }
    }

    private static decimal Clamp01(decimal value) => Math.Min(1m, Math.Max(0m, value));

    private int GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return 1;
        var claim = user.FindFirst("Id") ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 1;
    }

    private static string BuildReason(bool isEmpty, decimal capacityFit, decimal occupancyFit)
    {
        if (!isEmpty && capacityFit >= 0.9m)
        {
            return "Cùng loại sản phẩm, cột đang mở và sức chứa gần vừa đủ.";
        }
        if (!isEmpty)
        {
            return "Cùng loại sản phẩm và ưu tiên hoàn thiện cột đang mở.";
        }
        if (capacityFit >= 0.9m)
        {
            return "Cột trống có sức chứa gần vừa đủ.";
        }
        return "Cột trống đáp ứng sức chứa và điều kiện lưu kho.";
    }
}

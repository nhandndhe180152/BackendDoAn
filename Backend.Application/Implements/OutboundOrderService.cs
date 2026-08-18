using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Share.Services;
using Backend.Application.Constants;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ phiếu xuất kho (OutboundOrder).
/// Flow: DRAFT → PICKING → PACKED → DISPATCHED/COMPLETED
/// CANCELLED là nhánh kết thúc thay thế.
/// ConfirmDispatchAsync là bước quan trọng nhất — thực hiện trong 1 DB transaction.
/// </summary>
public class OutboundOrderService : IOutboundOrderService
{
    private readonly IOutboundOrderRepository _outboundOrderRepository;
    private readonly IRepositoryBase<OutboundOrderStatus, int> _outboundStatusRepository;
    private readonly IRepositoryBase<OutboundOrderItemAllocation, int> _allocationRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IRepositoryBase<SalesOrderStatus, int> _salesOrderStatusRepository;
    private readonly IPartyDebtRepository _partyDebtRepository;
    private readonly IDebtTransactionRepository _debtTransactionRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IScheduledJobService? _scheduledJobService;
    private readonly IDebtAgingCalculationService? _debtAgingService;
    private readonly IRepositoryBase<PaddyLotBag, int>? _bagRepository;
    private readonly IRepositoryBase<PaddyLotBagContent, int>? _bagContentRepository;
    private readonly IRepositoryBase<PaddyLotBagMovement, int>? _bagMovementRepository;
    private readonly ILocationRepository? _locationRepository;

    public OutboundOrderService(
        IOutboundOrderRepository outboundOrderRepository,
        IRepositoryBase<OutboundOrderStatus, int> outboundStatusRepository,
        IRepositoryBase<OutboundOrderItemAllocation, int> allocationRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        ISalesOrderRepository salesOrderRepository,
        IRepositoryBase<SalesOrderStatus, int> salesOrderStatusRepository,
        IPartyDebtRepository partyDebtRepository,
        IDebtTransactionRepository debtTransactionRepository,
        IHttpContextAccessor httpContextAccessor,
        IPaddyLotRepository paddyLotRepository,
        INotificationDispatcher notificationDispatcher,
        IScheduledJobService? scheduledJobService = null,
        IDebtAgingCalculationService? debtAgingService = null,
        IRepositoryBase<PaddyLotBag, int>? bagRepository = null,
        IRepositoryBase<PaddyLotBagContent, int>? bagContentRepository = null,
        IRepositoryBase<PaddyLotBagMovement, int>? bagMovementRepository = null,
        ILocationRepository? locationRepository = null)
    {
        _outboundOrderRepository       = outboundOrderRepository;
        _outboundStatusRepository      = outboundStatusRepository;
        _allocationRepository          = allocationRepository;
        _inventoryRepository           = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _salesOrderRepository          = salesOrderRepository;
        _salesOrderStatusRepository    = salesOrderStatusRepository;
        _partyDebtRepository           = partyDebtRepository;
        _debtTransactionRepository     = debtTransactionRepository;
        _httpContextAccessor           = httpContextAccessor;
        _paddyLotRepository            = paddyLotRepository;
        _notificationDispatcher        = notificationDispatcher;
        _scheduledJobService           = scheduledJobService;
        _debtAgingService              = debtAgingService;
        _bagRepository                 = bagRepository;
        _bagContentRepository          = bagContentRepository;
        _bagMovementRepository         = bagMovementRepository;
        _locationRepository            = locationRepository;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;

    private async Task<int> GetOutboundStatusIdAsync(string code)
    {
        var s = await _outboundStatusRepository.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"OutboundOrderStatus with code '{code}' not found.");
    }

    private async Task<int> GetSalesStatusIdAsync(string code)
    {
        var s = await _salesOrderStatusRepository.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"SalesOrderStatus with code '{code}' not found.");
    }

    private static string? FormatLocationCode(Location? location)
    {
        if (location == null)
            return null;

        if (!string.IsNullOrWhiteSpace(location.SlotCode))
            return location.SlotCode.Trim();

        var coordinates = new[] { location.ShelfRow, location.ShelfLevel }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(location.ZoneName))
        {
            var zoneName = location.ZoneName.Trim();
            return coordinates.Count > 0
                ? $"{zoneName} : {string.Join("/", coordinates)}"
                : zoneName;
        }

        if (coordinates.Count > 0)
            return string.Join("/", coordinates);

        if (!string.IsNullOrWhiteSpace(location.QrCode))
            return location.QrCode.Trim();

        return $"Vị trí #{location.Id}";
    }

    private static IEnumerable<OutboundOrderItemAllocation> ActiveAllocations(OutboundOrderItem item)
        => item.Allocations.Where(a => !a.IsDeleted).OrderBy(a => a.Id);

    private static string AppendBoundedNote(
        string? currentNote,
        string entry,
        int maxLength = OutboundOrderConstants.NoteMaxLength)
    {
        var original = currentNote?.TrimEnd() ?? string.Empty;
        if (original.Length >= maxLength)
            return original[..maxLength];

        var separator = original.Length > 0 ? "\n" : string.Empty;
        var availableForEntry = maxLength - original.Length - separator.Length;
        if (availableForEntry <= 0)
            return original;

        // Note là dữ liệu do người dùng nhập. Khi không đủ chỗ, chỉ cắt phần nhật ký
        // kỹ thuật được thêm vào, tuyệt đối không xóa phần đầu của ghi chú gốc.
        var boundedEntry = entry.Length <= availableForEntry
            ? entry
            : entry[..availableForEntry];
        return $"{original}{separator}{boundedEntry}";
    }

    private async Task<Location> GetOutboundStagingLocationAsync(int warehouseId)
    {
        if (_locationRepository == null)
            throw new InvalidOperationException("Chưa cấu hình kho chờ xuất cho nghiệp vụ đóng gói.");

        var staging = await _locationRepository.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId && x.IsOutboundStaging && x.IsActive && !x.IsDeleted, true);
        if (staging != null) return staging;
        throw new InvalidOperationException(
            $"Kho #{warehouseId} chưa được cấu hình khu chờ xuất. Vui lòng liên hệ quản trị viên.");
    }

    private async Task ReleaseOutboundColumnLocksAsync(
        OutboundOrder order, DateTime now, int userId, IReadOnlyCollection<int>? excludedLocationIds = null)
    {
        if (_locationRepository == null) return;
        await _locationRepository.ReleaseOutboundLocksAsync(order.Id, now, userId, excludedLocationIds);
    }

    public async Task<ApiResponse> ForceUnlockAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse.UnprocessableEntity("Phải nhập lý do mở khóa cột.");
        reason = reason.Trim();
        if (reason.Length > OutboundOrderConstants.ManualUnlockReasonMaxLength)
            return ApiResponse.UnprocessableEntity(
                $"Lý do mở khóa cột không được vượt quá {OutboundOrderConstants.ManualUnlockReasonMaxLength} ký tự.");
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy phiếu xuất.");
        if (_locationRepository == null)
            return ApiResponse.Conflict("Chưa cấu hình quản lý khóa cột.");

        var userId = GetCurrentUserId();
        var now = DateTimeHelper.VietnamNow();
        var released = await _locationRepository.ReleaseOutboundLocksAsync(id, now, userId);
        order.Note = AppendBoundedNote(order.Note, $"Mở khóa cột thủ công: {reason}");
        order.LastModifiedDate = now;
        order.UpdatedBy = userId;
        await _outboundOrderRepository.UpdateAsync(order);
        await _outboundOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(new { ReleasedLocationCount = released },
            released > 0 ? "Đã mở khóa cột thủ công." : "Phiếu không còn cột đang khóa.");
    }

    private static OutboundOrderDetailDto MapDetail(OutboundOrder o)
    {
        return new OutboundOrderDetailDto
        {
            Id                   = o.Id,
            SalesOrderId         = o.SalesOrderId,
            SOCode               = o.SalesOrder?.SOCode ?? "",
            CustomerId           = o.SalesOrder?.CustomerId ?? 0,
            CustomerName         = o.SalesOrder?.Customer?.Name ?? "",
            OutboundStatusId     = o.OutboundOrderStatusId,
            OutboundStatusName   = o.OutboundOrderStatus?.Name ?? "",
            OutboundStatusCode   = o.OutboundOrderStatus?.Code ?? "",
            OutboundStatusColor  = o.OutboundOrderStatus?.Color ?? "",
            WarehouseId          = o.WarehouseId,
            WarehouseName        = o.Warehouse?.Name ?? "",
            TotalDispatchedValue = o.TotalDispatchedValue,
            TotalDispatchedSaleValue = o.TotalDispatchedSaleValue,
            CompletedDate        = o.CompletedDate,
            Note                 = o.Note,
            CancelReason         = o.CancelReason,
            PackingScaleDevice   = o.PackingScaleDevice,
            PackedDate           = o.PackedDate,
            CreatedDate          = o.CreatedDate,
            Items = o.OutboundOrderItems.Where(i => !i.IsDeleted).Select(i => new OutboundOrderItemDto
            {
                Id               = i.Id,
                ProductVariantId = i.ProductVariantId,
                ProductVariantName = i.ProductVariant?.Name ?? "",
                SKU              = i.ProductVariant?.SKU,
                QuantityOrdered  = i.QuantityOrdered,
                QuantityPicked   = i.QuantityPicked,
                UnitCostPrice    = i.UnitCostPrice,
                SalesOrderItemId = i.SalesOrderItemId,
                Note             = i.Note,
                ActualWeightKg   = i.ActualWeightKg,
                ActualWeightSource = i.ActualWeightSource,
                Allocations = i.Allocations.Where(a => !a.IsDeleted).OrderBy(a => a.Id).Select(a => new OutboundOrderItemAllocationDto
                {
                    Id                = a.Id,
                    InventoryId       = a.InventoryId,
                    PaddyLotId        = a.PaddyLotId,
                    PaddyLotCode      = a.PaddyLot?.LotCode,
                    LocationId        = a.LocationId,
                    LocationCode      = FormatLocationCode(a.Location),
                    QuantityAllocated = a.QuantityAllocated,
                    QuantityPicked    = a.QuantityPicked,
                    UnitCostPrice     = a.UnitCostPrice
                }).ToList(),
                AllocationGroups = i.Allocations
                    .Where(a => !a.IsDeleted)
                    .GroupBy(a => new
                    {
                        a.InventoryId,
                        a.PaddyLotId,
                        a.LocationId,
                        a.QuantityAllocated
                    })
                    .OrderBy(group => group.Min(a => a.Id))
                    .Select(group => new OutboundOrderAllocationGroupDto
                    {
                        GroupKey = $"{group.Key.InventoryId}:{group.Key.LocationId}:{group.Key.QuantityAllocated:0.###}",
                        AllocationIds = group.OrderBy(a => a.Id).Select(a => a.Id).ToList(),
                        InventoryId = group.Key.InventoryId,
                        PaddyLotId = group.Key.PaddyLotId,
                        PaddyLotCode = group.Select(a => a.PaddyLot != null ? a.PaddyLot.LotCode : null).FirstOrDefault(),
                        LocationId = group.Key.LocationId,
                        LocationCode = FormatLocationCode(group.Select(a => a.Location).FirstOrDefault()),
                        BagCount = group.Count(),
                        WeightPerBagKg = group.Key.QuantityAllocated,
                        TotalAllocatedKg = group.Sum(a => a.QuantityAllocated),
                        TotalPickedKg = group.Sum(a => a.QuantityPicked)
                    }).ToList()
            }).ToList()
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetPagedAsync(OutboundOrderPagedQuery query)
    {
        // Chuẩn hóa tham số trang để client gửi page=0 hay pageSize âm không làm
        // vỡ Skip/Take.
        var page     = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;
        var skip     = (page - 1) * pageSize;

        var total = await _outboundOrderRepository.CountAsync(
            query.Keyword, query.OutboundStatusId);
        var list  = await _outboundOrderRepository.GetPagedListAsync(
            query.Keyword, skip, pageSize, query.OutboundStatusId);

        var dtos = list.Select(o => new OutboundOrderListDto
        {
            Id                   = o.Id,
            SalesOrderId         = o.SalesOrderId,
            SOCode               = o.SalesOrder?.SOCode ?? "",
            CustomerName         = o.SalesOrder?.Customer?.Name ?? "",
            OutboundStatusId     = o.OutboundOrderStatusId,
            OutboundStatusName   = o.OutboundOrderStatus?.Name ?? "",
            OutboundStatusCode   = o.OutboundOrderStatus?.Code ?? "",
            OutboundStatusColor  = o.OutboundOrderStatus?.Color ?? "",
            WarehouseId          = o.WarehouseId,
            WarehouseName        = o.Warehouse?.Name,
            TotalDispatchedValue = o.TotalDispatchedValue,
            TotalDispatchedSaleValue = o.TotalDispatchedSaleValue,
            CompletedDate        = o.CompletedDate,
            Note                 = o.Note,
            CancelReason         = o.CancelReason,
            CreatedDate          = o.CreatedDate
        }).ToList();

        return ApiResponse.Success(new { Total = total, Items = dtos });
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var o = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (o == null || o.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        return ApiResponse.Success(MapDetail(o));
    }

    public async Task<ApiResponse> GetAllocationCandidatesAsync(int id)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Draft)
            return ApiResponse.Conflict("Chỉ có thể xem nguồn phân bổ khi phiếu xuất đang ở trạng thái Nháp.", ApiCodeConstants.OutboundOrder.InvalidState);

        var variantIds = order.OutboundOrderItems.Where(x => !x.IsDeleted).Select(x => x.ProductVariantId).Distinct().ToList();
        var inventories = await _inventoryRepository.FindByCondition(x =>
                !x.IsDeleted && x.WarehouseId == order.WarehouseId && variantIds.Contains(x.ProductVariantId) &&
                x.LocationId.HasValue && x.QuantityOnHand > 0 &&
                x.Location != null && !x.Location.IsDeleted && x.Location.IsActive &&
                !x.Location.IsQuarantine && !x.Location.IsOutboundStaging &&
                !x.Location.OutboundLockOrderId.HasValue,
                false, x => x.Location, x => x.PaddyLot, x => x.PaddyLot.Status)
            .ToListAsync();

        var ownTransactions = await _inventoryTransactionRepository.FindByCondition(x =>
                x.ReferenceType == InventoryReferenceTypeConstants.SalesOrder &&
                x.ReferenceId == order.SalesOrderId &&
                (x.TransactionType == InventoryTransactionTypeConstants.Reserve ||
                 x.TransactionType == InventoryTransactionTypeConstants.ReleaseReserve))
            .ToListAsync();
        var ownReserved = ownTransactions
            .GroupBy(x => x.InventoryId)
            .ToDictionary(g => g.Key, g => Math.Max(0m, g.Sum(x => x.Quantity)));

        // ── Query bag data per location for Stack Card UX ──────────────
        var locationIds = inventories
            .Where(x => x.LocationId.HasValue)
            .Select(x => x.LocationId!.Value)
            .Distinct()
            .ToList();

        var bagsByLocation = new Dictionary<int, List<PaddyLotBag>>();
        if (_bagRepository != null && locationIds.Count > 0)
        {
            var bags = await _bagRepository
                .FindByCondition(x => x.LocationId.HasValue && locationIds.Contains(x.LocationId!.Value)
                    && x.Status == PaddyLotBagStatuses.Stored && x.WeightKg > 0 && !x.IsDeleted, true)
                .Include(x => x.Contents)
                .OrderBy(x => x.LocationId)
                .ThenByDescending(x => x.StackOrder)
                .ToListAsync();
            foreach (var group in bags.GroupBy(x => x.LocationId!.Value))
                bagsByLocation[group.Key] = group.ToList();
        }

        var result = inventories
            .Where(inv => inv.PaddyLot == null ||
                (inv.PaddyLot.Status != null && inv.PaddyLot.Status.IsSellable &&
                 inv.PaddyLot.Status.Code != LotStatusCodeConstants.Quarantine))
            .Select(inv =>
            {
                var own = ownReserved.GetValueOrDefault(inv.Id);
                var reservedByOthers = Math.Max(0m, inv.QuantityReserved - own);
                var selectableQuantity = Math.Max(0m, inv.QuantityOnHand - reservedByOthers);

                decimal? standardWeightKg = null;
                int fullBagCount = 0;
                bool hasOpenBag = false;
                decimal openBagWeightKg = 0;
                int? openBagId = null;
                bool isOpenBagBlocked = false;

                if (inv.LocationId.HasValue && bagsByLocation.TryGetValue(inv.LocationId.Value, out var locationBags))
                {
                    var lotBags = locationBags
                        .Select(b => new
                        {
                            Bag = b,
                            LotWeightKg = b.Contents
                                .Where(c => c.LotId == inv.PaddyLotId && !c.IsDeleted)
                                .Sum(c => c.WeightKg),
                            ActiveWeightKg = b.Contents
                                .Where(c => !c.IsDeleted)
                                .Sum(c => c.WeightKg)
                        })
                        .Where(x => x.LotWeightKg > 0.0005m)
                        .ToList();
                    if (lotBags.Count > 0)
                    {
                        standardWeightKg = lotBags.FirstOrDefault(x => x.Bag.StandardWeightKg.HasValue)?.Bag.StandardWeightKg;
                        fullBagCount = 0;

                        var openBag = lotBags.FirstOrDefault(x => !x.Bag.IsFull);
                        if (openBag != null)
                        {
                            hasOpenBag = true;
                            openBagWeightKg = Math.Min(openBag.LotWeightKg, selectableQuantity);
                            openBagId = openBag.Bag.Id;

                            var topStackOrder = locationBags.Max(b => b.StackOrder);
                            isOpenBagBlocked = openBag.Bag.StackOrder != topStackOrder;
                        }

                        foreach (var stackBag in locationBags.OrderByDescending(x => x.StackOrder))
                        {
                            var lotWeightKg = stackBag.Contents
                                .Where(c => c.LotId == inv.PaddyLotId && !c.IsDeleted)
                                .Sum(c => c.WeightKg);
                            if (lotWeightKg <= 0.0005m)
                                break;

                            var activeWeightKg = stackBag.Contents
                                .Where(c => !c.IsDeleted)
                                .Sum(c => c.WeightKg);
                            if (Math.Abs(lotWeightKg - activeWeightKg) > 0.0005m)
                                break;

                            if (stackBag.IsFull)
                                fullBagCount++;
                        }

                        if (standardWeightKg.HasValue && standardWeightKg.Value > 0)
                        {
                            var selectableForFullBags = Math.Max(0m, selectableQuantity - (hasOpenBag && !isOpenBagBlocked ? openBagWeightKg : 0m));
                            var maxFullBagsBySelectable = (int)Math.Floor(selectableForFullBags / standardWeightKg.Value);
                            fullBagCount = Math.Min(fullBagCount, maxFullBagsBySelectable);
                        }
                    }
                }

                return new OutboundAllocationCandidateDto
                {
                    InventoryId = inv.Id,
                    ProductVariantId = inv.ProductVariantId,
                    PaddyLotId = inv.PaddyLotId,
                    LotCode = inv.PaddyLot?.LotCode,
                    LocationId = inv.LocationId,
                    LocationCode = FormatLocationCode(inv.Location),
                    QuantityOnHand = inv.QuantityOnHand,
                    ReservedForThisSalesOrder = own,
                    ReservedByOtherOrders = reservedByOthers,
                    SelectableQuantity = selectableQuantity,
                    StandardWeightKg = standardWeightKg,
                    FullBagCount = fullBagCount,
                    HasOpenBag = hasOpenBag,
                    OpenBagWeightKg = openBagWeightKg,
                    OpenBagId = openBagId,
                    IsOpenBagBlocked = isOpenBagBlocked
                };
            })
            .Where(x => x.SelectableQuantity > 0)
            .OrderBy(x => x.LocationCode).ThenBy(x => x.LotCode)
            .ToList();

        return ApiResponse.Success(result);
    }


    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phân bổ lot/vị trí cho từng OutboundOrderItem → trạng thái PICKING.
    /// </summary>
    public async Task<ApiResponse> AllocateAsync(int id, AllocateOutboundDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Draft)
            return ApiResponse.Conflict(
                $"Phiếu xuất đang ở trạng thái '{order.OutboundOrderStatus?.Name}', chỉ có thể phân bổ khi ở DRAFT.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        await using var allocationTx = await _outboundOrderRepository.BeginTransactionAsync();

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        var locationIdsToLock = new HashSet<int>();

        var requestedByItem = dto.Allocations
            .GroupBy(x => x.OutboundOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Lots.Sum(l => l.QuantityAllocated)));
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            var total = ActiveAllocations(item).Sum(a => a.QuantityAllocated)
                        + requestedByItem.GetValueOrDefault(item.Id);
            if (Math.Abs(total - item.QuantityOrdered) > 0.001m)
                return ApiResponse.UnprocessableEntity(
                    $"Sản phẩm '{item.ProductVariant?.Name}' phải được phân bổ đủ {item.QuantityOrdered:0.###} kg trước khi bắt đầu lấy hàng. Hiện đã phân bổ {total:0.###} kg.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);
        }

        foreach (var allocItem in dto.Allocations)
        {
            var item = order.OutboundOrderItems.FirstOrDefault(i => !i.IsDeleted && i.Id == allocItem.OutboundOrderItemId);
            if (item == null)
                return ApiResponse.BadRequest(
                    $"OutboundOrderItem ID {allocItem.OutboundOrderItemId} không tồn tại trong phiếu.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            // Validate tổng quantity allocated không vượt quantity ordered cho item này
            var alreadyAllocated = ActiveAllocations(item).Sum(a => a.QuantityAllocated);
            var newAllocTotal    = allocItem.Lots.Sum(l => l.QuantityAllocated);
            if (alreadyAllocated + newAllocTotal > item.QuantityOrdered)
                return ApiResponse.UnprocessableEntity(
                    $"Tổng số lượng phân bổ ({alreadyAllocated + newAllocTotal}) vượt quá số lượng đặt ({item.QuantityOrdered}) " +
                    $"cho OutboundOrderItem ID {item.Id}.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            // Giải phóng reservation của Sales Order (theo FIFO) tương ứng với số lượng đang phân bổ
            if (order.SalesOrderId > 0)
            {
                var reserveTxs = await _inventoryTransactionRepository
                    .FindByCondition(x => x.ReferenceType == InventoryReferenceTypeConstants.SalesOrder 
                                       && x.ReferenceId == order.SalesOrderId 
                                       && x.ProductVariantId == item.ProductVariantId
                                       && x.TransactionType == InventoryTransactionTypeConstants.Reserve)
                    .ToListAsync();

                var releaseTxs = await _inventoryTransactionRepository
                    .FindByCondition(x => x.ReferenceType == InventoryReferenceTypeConstants.SalesOrder 
                                       && x.ReferenceId == order.SalesOrderId 
                                       && x.ProductVariantId == item.ProductVariantId
                                       && x.TransactionType == InventoryTransactionTypeConstants.ReleaseReserve)
                    .ToListAsync();

                var netReserves = reserveTxs
                    .GroupBy(x => x.InventoryId)
                    .Select(g => new {
                        InventoryId = g.Key,
                        Net = g.Sum(x => x.Quantity) + releaseTxs.Where(r => r.InventoryId == g.Key).Sum(r => r.Quantity)
                    })
                    .Where(x => x.Net > 0)
                    .ToList();

                decimal remainingToUnreserve = newAllocTotal;

                foreach (var res in netReserves)
                {
                    if (remainingToUnreserve <= 0) break;

                    var take = Math.Min(res.Net, remainingToUnreserve);
                    
                    var invToUnreserve = await _inventoryRepository.GetByIdAsync(res.InventoryId);
                    if (invToUnreserve != null)
                    {
                        var before = invToUnreserve.QuantityReserved;
                        invToUnreserve.QuantityReserved = Math.Max(0, invToUnreserve.QuantityReserved - take);
                        invToUnreserve.LastModifiedDate = now;
                        await _inventoryRepository.UpdateAsync(invToUnreserve);

                        var unreserveTx = new InventoryTransaction
                        {
                            InventoryId = invToUnreserve.Id,
                            WarehouseId = invToUnreserve.WarehouseId,
                            LocationId = invToUnreserve.LocationId,
                            ProductVariantId = invToUnreserve.ProductVariantId,
                            PaddyLotId = invToUnreserve.PaddyLotId,
                            TransactionType = InventoryTransactionTypeConstants.ReleaseReserve,
                            ReferenceType = InventoryReferenceTypeConstants.SalesOrder,
                            ReferenceId = order.SalesOrderId,
                            ReferenceItemId = item.SalesOrderItemId,
                            Quantity = -take,
                            BeforeQuantity = before,
                            AfterQuantity = invToUnreserve.QuantityReserved,
                            WeightKg = -take,
                            Note = $"Giải phóng tồn tạm giữ do phân bổ OutboundOrder {order.Id}",
                            CreatedDate = now,
                            CreatedBy = userId
                        };
                        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(unreserveTx);
                    }
                    remainingToUnreserve -= take;
                }
            }

            List<AllocateItemLotDto> physicalLots;
            try
            {
                physicalLots = await BuildRequestedBagAllocationsAsync(
                    item.ProductVariantId, order.WarehouseId, allocItem.Lots);
            }
            catch (InvalidOperationException ex)
            {
                return ApiResponse.UnprocessableEntity(ex.Message, ApiCodeConstants.OutboundOrder.InvalidRequest);
            }

            // Tính weighted-average cost trước khi tạo allocation
            decimal totalAllocQty   = alreadyAllocated;
            decimal weightedCostSum = alreadyAllocated * item.UnitCostPrice;

            // Hàng có lớp bao được phân bổ theo đúng bao/content sẽ lấy; danh sách inventory từ client
            // chỉ còn là fallback cho dữ liệu cũ chưa bag-track.
            var lotsToAllocate = physicalLots.Count > 0 ? physicalLots : allocItem.Lots;

            var inventoryIdsToAllocate = lotsToAllocate.Select(x => x.InventoryId).Distinct().ToList();
            var inventoriesToAllocate = await _inventoryRepository.FindByCondition(x =>
                    inventoryIdsToAllocate.Contains(x.Id) && !x.IsDeleted,
                    false, x => x.Location, x => x.PaddyLot, x => x.PaddyLot.Status)
                .ToDictionaryAsync(x => x.Id);

            foreach (var lot in lotsToAllocate)
            {
                if (!inventoriesToAllocate.TryGetValue(lot.InventoryId, out var inv))
                    return ApiResponse.BadRequest(
                        $"Inventory ID {lot.InventoryId} không tồn tại.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                if (inv.LocationId == null)
                    return ApiResponse.BadRequest(
                        $"Inventory ID {lot.InventoryId} chưa có vị trí.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                if (inv.Location?.IsOutboundStaging == true)
                    return ApiResponse.UnprocessableEntity(
                        $"Vị trí {FormatLocationCode(inv.Location)} là khu chờ xuất, không thể dùng để phân bổ hàng mới.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                if (inv.Location?.OutboundLockOrderId is int lockingOrderId && lockingOrderId != order.Id)
                    return ApiResponse.Conflict(
                        $"Cột '{FormatLocationCode(inv.Location)}' đang được khóa bởi phiếu xuất #{lockingOrderId}. Vui lòng chọn cột khác.",
                        ApiCodeConstants.OutboundOrder.ConcurrencyConflict);

                if (inv.Location != null)
                {
                    locationIdsToLock.Add(inv.Location.Id);
                }

                if (inv.ProductVariantId != item.ProductVariantId)
                    return ApiResponse.BadRequest(
                        $"Sản phẩm của Inventory ID {lot.InventoryId} không khớp với yêu cầu.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                if (inv.WarehouseId != order.WarehouseId)
                    return ApiResponse.BadRequest(
                        $"Inventory ID {lot.InventoryId} không thuộc kho xuất.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                // C1: Kiểm tra xem tồn kho có bị cách ly hay không
                var isQuarantined = (inv.Location != null && inv.Location.IsQuarantine)
                    || (inv.PaddyLot != null && inv.PaddyLot.Status != null && inv.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine);

                if (isQuarantined || (inv.PaddyLot != null && inv.PaddyLot.Status != null && !inv.PaddyLot.Status.IsSellable))
                    return ApiResponse.UnprocessableEntity(
                        $"Dòng tồn kho ID {lot.InventoryId} nằm ở vị trí cách ly hoặc thuộc lô hàng không hợp lệ để phân bổ.",
                        ApiCodeConstants.OutboundOrder.LotQuarantined);

                // C2: Kiểm tra tồn khả dụng thực tế của inventory row này
                var availableQty = inv.QuantityOnHand - inv.QuantityReserved;
                if (lot.QuantityAllocated > availableQty)
                    return ApiResponse.UnprocessableEntity(
                        $"Inventory ID {lot.InventoryId} chỉ còn {availableQty} kg khả dụng, " +
                        $"không đủ để phân bổ {lot.QuantityAllocated} kg.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                await _allocationRepository.CreateAsync(new OutboundOrderItemAllocation
                {
                    OutboundOrderItemId = item.Id,
                    InventoryId         = inv.Id,
                    PaddyLotId          = inv.PaddyLotId,
                    LocationId          = inv.LocationId.Value,
                    QuantityAllocated   = lot.QuantityAllocated,
                    QuantityPicked      = 0,
                    UnitCostPrice       = inv.CostPrice,
                    CreatedDate         = now,
                    CreatedBy           = userId
                });

                // C1: Tăng QuantityReserved trên đúng inventory row được chọn
                inv.QuantityReserved  += lot.QuantityAllocated;
                if (inv.QuantityReserved > inv.QuantityOnHand)
                    inv.QuantityReserved = inv.QuantityOnHand; // safety clamp
                inv.LastModifiedDate   = now;
                inv.UpdatedBy          = userId;
                await _inventoryRepository.UpdateAsync(inv);

                // M3: Tích lũy để tính weighted-average cost
                weightedCostSum += lot.QuantityAllocated * inv.CostPrice;
                totalAllocQty   += lot.QuantityAllocated;
            }

            // M3: Gán weighted-average cost cho OutboundOrderItem
            item.UnitCostPrice = totalAllocQty > 0
                ? Math.Round(weightedCostSum / totalAllocQty, 2)
                : item.UnitCostPrice;
        }

        var expiresBefore = now - OutboundOrderConstants.ColumnLockTimeout;
        var expiredLockOrderIds = _locationRepository == null
            ? new List<int>()
            : await _locationRepository.FindByCondition(x =>
                    locationIdsToLock.Contains(x.Id) &&
                    x.OutboundLockOrderId.HasValue && x.OutboundLockOrderId != order.Id &&
                    x.OutboundLockedAt.HasValue && x.OutboundLockedAt <= expiresBefore,
                    false)
                .Select(x => x.OutboundLockOrderId!.Value)
                .Distinct()
                .ToListAsync();

        // Repository tự chuẩn hóa danh sách ID bằng Distinct(). ExecuteUpdateAsync bỏ qua
        // change tracker; không được ghi lại các Location đang được theo dõi sau điểm này
        // nếu chưa reload, vì có thể vô tình xóa khóa vừa đặt trong DB.
        if (_locationRepository == null ||
            await _locationRepository.TryLockForOutboundAsync(locationIdsToLock, order.Id, now, userId) != locationIdsToLock.Count)
            return ApiResponse.Conflict(
                "Một hoặc nhiều cột vừa được phiếu khác sử dụng. Vui lòng tải lại và phân bổ lại.",
                ApiCodeConstants.OutboundOrder.ConcurrencyConflict);

        if (expiredLockOrderIds.Count > 0)
        {
            order.Note = AppendBoundedNote(
                order.Note,
                $"Tự động tiếp quản khóa cột quá {OutboundOrderConstants.ColumnLockTimeout.TotalHours:0} giờ " +
                $"từ phiếu xuất: {string.Join(", ", expiredLockOrderIds.Select(x => $"#{x}"))}.");
        }

        order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Picking);
        order.LastModifiedDate      = now;
        order.UpdatedBy             = userId;
        await _outboundOrderRepository.UpdateAsync(order);

        try
        {
            await _outboundOrderRepository.SaveChangesAsync();
            await allocationTx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await allocationTx.RollbackAsync();
            return ApiResponse.Conflict(
                "Tồn kho đã thay đổi trong lúc phân bổ. Vui lòng tải lại và thử lại.",
                ApiCodeConstants.OutboundOrder.ConcurrencyConflict);
        }

        return ApiResponse.Success(message: "Phân bổ lot/vị trí thành công. Trạng thái: PICKING.");
    }

    /// <summary>
    /// Cập nhật số lượng thực tế đã lấy cho từng allocation.
    /// </summary>
    public async Task<ApiResponse> PickAsync(int id, PickOutboundDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Picking)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái PICKING để cập nhật picking.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        foreach (var pickDto in dto.Picks)
        {
            var allocation = order.OutboundOrderItems
                .Where(i => !i.IsDeleted)
                .SelectMany(ActiveAllocations)
                .FirstOrDefault(a => a.Id == pickDto.AllocationId);

            if (allocation == null)
                return ApiResponse.BadRequest(
                    $"Allocation ID {pickDto.AllocationId} không tồn tại.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            if (pickDto.QuantityPicked > allocation.QuantityAllocated)
                return ApiResponse.UnprocessableEntity(
                    $"Số lượng lấy ({pickDto.QuantityPicked}) không được vượt quá số lượng đã phân bổ ({allocation.QuantityAllocated}).",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            allocation.QuantityPicked  = pickDto.QuantityPicked;
            allocation.LastModifiedDate = now;
            allocation.UpdatedBy       = userId;
            await _allocationRepository.UpdateAsync(allocation);
        }

        // Cập nhật QuantityPicked tổng cho mỗi OutboundOrderItem
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            item.QuantityPicked  = ActiveAllocations(item).Sum(a => a.QuantityPicked);
            item.LastModifiedDate = now;
            item.UpdatedBy       = userId;
        }

        order.LastModifiedDate = now;
        order.UpdatedBy        = userId;
        await _outboundOrderRepository.UpdateAsync(order);
        await _outboundOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Cập nhật số lượng picking thành công.");
    }

    public async Task<ApiResponse> ConfirmPackingAsync(int id, ConfirmPackingDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Picking)
            return ApiResponse.Conflict(
                "Phiếu xuất phải ở trạng thái PICKING để xác nhận đóng gói.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        if (string.IsNullOrWhiteSpace(dto.QrCode))
            return ApiResponse.BadRequest("Mã QR không hợp lệ.", ApiCodeConstants.OutboundOrder.InvalidRequest);

        // Validate mỗi item đã pick ít nhất bằng qty ordered
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            var picked = ActiveAllocations(item).Sum(a => a.QuantityPicked);
            if (Math.Abs(picked - item.QuantityOrdered) > 0.001m)
                return ApiResponse.UnprocessableEntity(
                    $"Sản phẩm '{item.ProductVariant?.Name}' chưa pick đủ. " +
                    $"Cần: {item.QuantityOrdered}, Đã lấy: {picked}.",
                    ApiCodeConstants.OutboundOrder.PickedQuantityMismatch);
        }

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        // Ghi nhận khối lượng đóng gói thực tế theo từng dòng.
        var itemsById = order.OutboundOrderItems
            .Where(i => !i.IsDeleted)
            .ToDictionary(i => i.Id);

        if (dto.Items is { Count: > 0 })
        {
            foreach (var line in dto.Items)
            {
                if (!itemsById.TryGetValue(line.OutboundOrderItemId, out var item))
                {
                    return ApiResponse.BadRequest(
                        $"Dòng phiếu xuất {line.OutboundOrderItemId} không thuộc phiếu này.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);
                }

                if (line.ActualWeightKg is < 0)
                {
                    return ApiResponse.BadRequest(
                        "Khối lượng thực tế không được âm.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);
                }
            }
        }
        else if (dto.ActualWeightKg is < 0)
        {
            return ApiResponse.BadRequest(
                "Khối lượng thực tế không được âm.",
                ApiCodeConstants.OutboundOrder.InvalidRequest);
        }

        await using var tx =
            await _outboundOrderRepository.BeginTransactionAsync();

        try
        {
            if (dto.Items is { Count: > 0 })
            {
                foreach (var line in dto.Items)
                {
                    var item = itemsById[line.OutboundOrderItemId];

                    item.ActualWeightKg = line.ActualWeightKg;
                    item.ActualWeightSource = line.ActualWeightKg == null
                        ? null
                        : NormalizeWeightSource(line.Source);
                    item.LastModifiedDate = now;
                    item.UpdatedBy = userId;
                }
            }
            else if (dto.ActualWeightKg is { } total && itemsById.Count == 1)
            {
                // Tương thích client cũ chỉ gửi một khối lượng tổng.
                var item = itemsById.Values.First();

                item.ActualWeightKg = total;
                item.ActualWeightSource = NormalizeWeightSource(
                    string.IsNullOrWhiteSpace(dto.ScaleDevice)
                        ? "MANUAL"
                        : "SCALE");
                item.LastModifiedDate = now;
                item.UpdatedBy = userId;
            }

            // Đóng gói chỉ chuyển hàng sang staging.
            // Tổng tồn vật lý toàn kho chỉ giảm khi dispatch.
            var staging =
                await GetOutboundStagingLocationAsync(order.WarehouseId);

            var partiallySplitLocationIds =
                await StagePhysicalBagsAsync(
                    order,
                    staging,
                    userId,
                    now);

            await TransferPackedInventoryToStagingAsync(
                order,
                staging,
                userId,
                now);

            // Cột còn bao nguồn bị tách tiếp tục bị khóa.
            // Các cột đã chuyển hết hàng sang staging được mở khóa.
            await ReleaseOutboundColumnLocksAsync(
                order,
                now,
                userId,
                partiallySplitLocationIds);

            order.PackingScaleDevice =
                string.IsNullOrWhiteSpace(dto.ScaleDevice)
                    ? null
                    : dto.ScaleDevice.Trim();

            order.PackedDate = now;
            order.OutboundOrderStatusId =
                await GetOutboundStatusIdAsync(
                    OutboundOrderStatusNames.Packed);
            order.LastModifiedDate = now;
            order.UpdatedBy = userId;

            await _outboundOrderRepository.UpdateAsync(order);
            await _outboundOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse.Success(message: "Đóng gói hoàn tất. Trạng thái: PACKED.");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse.UnprocessableEntity(ex.Message, ApiCodeConstants.OutboundOrder.InvalidRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>Chỉ chấp nhận SCALE; mọi giá trị khác coi như nhập tay.</summary>
    private static string NormalizeWeightSource(string? source) =>
        string.Equals(source?.Trim(), "SCALE", StringComparison.OrdinalIgnoreCase)
            ? "SCALE"
            : "MANUAL";

    /// <summary>
    /// Xác nhận xuất kho — bước quan trọng nhất.
    /// Toàn bộ logic chạy trong 1 DB transaction thật:
    /// Giảm tồn, tạo InventoryTransaction, cập nhật SalesOrder, tạo công nợ nếu chưa trả đủ.
    /// </summary>
    public async Task<ApiResponse> ConfirmDispatchAsync(int id, ConfirmDispatchDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        // 1. Validate trạng thái
        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Packed)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái PACKED để xác nhận xuất kho. " +
                $"Trạng thái hiện tại: {order.OutboundOrderStatus?.Name}.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        // 2. Validate lot còn sellable
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            foreach (var alloc in ActiveAllocations(item))
            {
                var lotIsQuarantined = (alloc.PaddyLot != null && alloc.PaddyLot.Status?.Code == LotStatusCodeConstants.Quarantine)
                    || (alloc.Location != null && alloc.Location.IsQuarantine);

                if (lotIsQuarantined || (alloc.PaddyLot != null && (alloc.PaddyLot.Status == null || !alloc.PaddyLot.Status.IsSellable)))
                    return ApiResponse.UnprocessableEntity(
                        $"Lô {alloc.PaddyLot?.LotCode ?? "không xác định"} nằm ở vị trí cách ly hoặc có trạng thái không hợp lệ để xuất bán.",
                        ApiCodeConstants.OutboundOrder.LotQuarantined);
            }
        }

        // 3. Validate qty picked
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            var picked = ActiveAllocations(item).Sum(a => a.QuantityPicked);
            if (Math.Abs(picked - item.QuantityOrdered) > 0.001m)
                return ApiResponse.UnprocessableEntity(
                    $"Sản phẩm '{item.ProductVariant?.Name}' chưa có số lượng thực lấy.",
                    ApiCodeConstants.OutboundOrder.PickedQuantityMismatch);
        }

        var now    = DateTimeHelper.VietnamNow();
        var amountToChargeOnDispatch = order.OutboundOrderItems
            .Where(i => !i.IsDeleted)
            .Sum(item =>
            {
                var unitSalePrice = item.SalesOrderItem != null && item.SalesOrderItem.QuantityOrdered > 0
                    ? item.SalesOrderItem.LineAmount / item.SalesOrderItem.QuantityOrdered
                    : item.SalesOrderItem?.UnitSalePrice ?? 0;
                return ActiveAllocations(item).Sum(a => a.QuantityPicked) * unitSalePrice;
            });

        if (amountToChargeOnDispatch > 0)
        {
            if (!dto.DueDate.HasValue)
                return ApiResponse.UnprocessableEntity("Vui lòng chọn hạn thanh toán trước khi xác nhận xuất kho có phát sinh công nợ.");
            if (dto.DueDate.Value.Date < now.Date)
                return ApiResponse.UnprocessableEntity("Hạn thanh toán không được trước ngày hiện tại.");
        }

        var userId = GetCurrentUserId();
        decimal totalDispatchedValue = 0;
        decimal totalDispatchedSaleValue = 0;
        PartyDebt? partyDebt = null;

        // Bọc toàn bộ trong 1 DB transaction thật — đảm bảo nguyên tử
        await using var tx = await _outboundOrderRepository.BeginTransactionAsync();
        try
        {
            var stagedAllocationIds = await ConsumeStagedBagsAsync(order.Id, userId, now);
            var staging = stagedAllocationIds.Count == 0 || _locationRepository == null
                ? null
                : await _locationRepository.FirstOrDefaultAsync(x =>
                    x.WarehouseId == order.WarehouseId && x.IsOutboundStaging && x.IsActive && !x.IsDeleted, true);
            if (stagedAllocationIds.Count > 0 && staging == null)
                throw new InvalidOperationException("Không tìm thấy khu chờ xuất của phiếu đã đóng gói.");

            var consumedLegacyAllocationIds = _bagMovementRepository == null
                ? new HashSet<int>()
                : (await _bagMovementRepository.FindByCondition(x =>
                        x.ReferenceType == InventoryReferenceTypeConstants.OutboundOrder &&
                        x.ReferenceId == order.Id &&
                        x.MovementType == PaddyLotBagMovementTypes.OutboundConsume &&
                        x.ReferenceItemId.HasValue && !x.IsDeleted, true)
                    .Select(x => x.ReferenceItemId!.Value)
                    .Distinct()
                    .ToListAsync()).ToHashSet();

            // 4. Với từng allocation: giảm tồn + tạo giao dịch
            foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
            {
                foreach (var alloc in ActiveAllocations(item))
                {
                    var sourceInventory = alloc.Inventory;
                    if (sourceInventory == null || sourceInventory.IsDeleted)
                    {
                        await tx.RollbackAsync();
                        return ApiResponse.BadRequest(
                            $"Inventory ID {alloc.InventoryId} không còn tồn tại.",
                            ApiCodeConstants.OutboundOrder.InvalidRequest);
                    }

                    var wasStaged = stagedAllocationIds.Contains(alloc.Id);
                    var inv = sourceInventory;
                    var occupancyLocation = alloc.Location;
                    if (wasStaged)
                    {
                        if (!alloc.PaddyLotId.HasValue)
                            throw new InvalidOperationException($"Allocation #{alloc.Id} ở staging không có thông tin lô.");
                        inv = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                                  sourceInventory.ProductVariantId, sourceInventory.WarehouseId,
                                  staging!.Id, alloc.PaddyLotId)
                              ?? throw new InvalidOperationException($"Không tìm thấy tồn staging cho allocation #{alloc.Id}.");
                        occupancyLocation = staging;
                    }
                    else if (alloc.PaddyLotId.HasValue && alloc.QuantityPicked > 0.0005m &&
                             !consumedLegacyAllocationIds.Contains(alloc.Id))
                    {
                        await ConsumePhysicalBagsAsync(alloc.PaddyLotId.Value, alloc.LocationId, alloc.QuantityPicked,
                            order.Id, alloc.Id, userId, now);
                    }

                    var before = inv.QuantityOnHand;

                    // 5. GIẢM TỒN KHO VẬT LÝ (Inventory):
                    // - Trừ số lượng thực xuất (QuantityPicked) khỏi tồn kho vật lý của kệ.
                    // - Trừ số lượng giữ trước (QuantityAllocated) khỏi hàng đang giữ trước.
                    inv.QuantityOnHand    -= alloc.QuantityPicked;
                    inv.QuantityReserved  -= alloc.QuantityAllocated;

                    // Không ép tồn âm, chặn và báo lỗi
                    if (inv.QuantityOnHand < 0) 
                    {
                        await tx.RollbackAsync();
                        return ApiResponse.UnprocessableEntity($"Tồn kho ID {inv.Id} không đủ để xuất.", ApiCodeConstants.OutboundOrder.InsufficientStock);
                    }
                    if (inv.QuantityReserved  < 0) inv.QuantityReserved  = 0;

                    inv.LastModifiedDate = now;
                    inv.UpdatedBy        = userId;
                    await _inventoryRepository.UpdateAsync(inv);

                    // ĐỒNG BỘ HÓA SỨC CHỨA VỊ TRÍ KỆ (Location occupancy):
                    if (occupancyLocation != null)
                    {
                        occupancyLocation.CurrentOccupancy = Math.Max(0m, occupancyLocation.CurrentOccupancy - alloc.QuantityPicked);
                        if (occupancyLocation.CurrentOccupancy == 0)
                        {
                            occupancyLocation.CurrentProductVariantId = null;
                        }
                        if (_locationRepository != null)
                            await _locationRepository.UpdateAsync(occupancyLocation);
                    }

                    // ĐỒNG BỘ HÓA TỒN LÔ HÀNG (PaddyLot):
                    // - Vì lô hàng thực tế đã xuất ra khỏi kho, khối lượng còn lại của lô (RemainingWeightKg)
                    //   phải được khấu trừ tương ứng với số lượng xuất kho vật lý.
                    // - Điều này đảm bảo tính đồng nhất giữa Dashboard (đọc từ PaddyLot) và Giám sát kho (đọc từ Inventory).
                    if (alloc.PaddyLotId.HasValue)
                    {
                        // PaddyLot đã được Include sẵn trong GetByIdDetailAsync (cùng DbContext ⇒ cùng
                        // instance tracked) → dùng trực tiếp, bỏ GetByIdAsync để tránh N+1 SELECT mỗi allocation.
                        var lot = alloc.PaddyLot;
                        if (lot != null && !lot.IsDeleted)
                        {
                            lot.RemainingWeightKg = Math.Max(0m, lot.RemainingWeightKg - alloc.QuantityPicked);
                            await _paddyLotRepository.UpdateAsync(lot);
                            await _paddyLotRepository.SaveChangesAsync();
                        }
                    }

                    // 6. GHI NHẬN LỊCH SỬ GIAO DỊCH TỒN KHO (InventoryTransaction)
                    // - Giao dịch xuất kho được lưu với giá trị lượng xuất âm (Quantity = -alloc.QuantityPicked)
                    var txn = new InventoryTransaction
                    {
                        InventoryId      = inv.Id,
                        WarehouseId      = inv.WarehouseId,
                        LocationId       = inv.LocationId,
                        ProductVariantId = inv.ProductVariantId,
                        TransactionType  = InventoryTransactionTypeConstants.Export,
                        ReferenceType    = InventoryReferenceTypeConstants.OutboundOrder,
                        ReferenceId      = order.Id,
                        ReferenceItemId  = alloc.Id,
                        PaddyLotId       = alloc.PaddyLotId,
                        Quantity         = -alloc.QuantityPicked,   // âm = xuất
                        BeforeQuantity   = before,
                        AfterQuantity    = inv.QuantityOnHand,
                        Note             = dto.Note,
                        CreatedDate      = now,
                        CreatedBy        = userId
                    };
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txn);

                    totalDispatchedValue += alloc.QuantityPicked * alloc.UnitCostPrice;
                    
                    var unitSalePrice = item.SalesOrderItem != null && item.SalesOrderItem.QuantityOrdered > 0
                        ? (item.SalesOrderItem.LineAmount / item.SalesOrderItem.QuantityOrdered)
                        : (item.SalesOrderItem?.UnitSalePrice ?? 0);
                    totalDispatchedSaleValue += alloc.QuantityPicked * unitSalePrice;
                }

                // 7. Cập nhật OutboundOrderItem.QuantityPicked
                item.QuantityPicked    = ActiveAllocations(item).Sum(a => a.QuantityPicked);
                item.LastModifiedDate  = now;
                item.UpdatedBy         = userId;
            }

            // 8. Cập nhật OutboundOrder → DISPATCHED
            order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Dispatched);
            order.TotalDispatchedValue  = totalDispatchedValue;
            order.TotalDispatchedSaleValue = totalDispatchedSaleValue;
            order.CompletedDate         = now;
            order.LastModifiedDate      = now;
            order.UpdatedBy             = userId;
            if (dto.Note != null) order.Note = dto.Note;
            await _outboundOrderRepository.UpdateAsync(order);

            // 9. Cập nhật SalesOrder → DELIVERING
            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(order.SalesOrderId);
            if (salesOrder != null && !salesOrder.IsDeleted)
            {
                salesOrder.StatusId         = await GetSalesStatusIdAsync(SalesOrderStatusNames.Delivering);
                salesOrder.LastModifiedDate = now;
                salesOrder.UpdatedBy        = userId;
                await _salesOrderRepository.UpdateAsync(salesOrder);

                // 10. Tạo công nợ phải thu dựa trên giá trị xuất kho của phiếu này
                var amountToCharge = order.TotalDispatchedSaleValue;
                partyDebt = null;
                if (amountToCharge > 0)
                {
                    var existingCharge = await _debtTransactionRepository.FirstOrDefaultAsync(x => 
                        x.RefType == "OUTBOUND_ORDER" && x.RefId == order.Id && x.TransactionType == LookupCodes.DebtTransactionType.Charge && !x.IsDeleted);
                    
                    if (existingCharge == null)
                    {
                        // Tìm hoặc tạo PartyDebt RECEIVABLE
                        partyDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
                            !x.IsDeleted &&
                            x.PartyType == "CUSTOMER" &&
                            x.PartyId == salesOrder.CustomerId &&
                            x.Direction == "RECEIVABLE" &&
                            x.IsActive);

                        if (partyDebt == null)
                        {
                            partyDebt = new PartyDebt
                            {
                                OrganizationId  = salesOrder.OrganizationId,
                                PartyType       = "CUSTOMER",
                                PartyId         = salesOrder.CustomerId,
                                Direction       = "RECEIVABLE",
                                OpeningBalance  = 0,
                                CurrentBalance  = 0,
                                IsActive        = true,
                                CreatedDate     = now,
                                CreatedBy       = userId
                            };
                            await _partyDebtRepository.CreateAsync(partyDebt);
                            await _outboundOrderRepository.SaveChangesAsync();
                        }

                        partyDebt.CurrentBalance  += amountToCharge;
                        partyDebt.LastModifiedDate  = now;
                        partyDebt.UpdatedBy         = userId;
                        await _partyDebtRepository.UpdateAsync(partyDebt);

                        // Tạo DebtTransaction CHARGE cho OutboundOrder
                        await _debtTransactionRepository.CreateAsync(new DebtTransaction
                        {
                            PartyDebtId     = partyDebt.Id,
                            TransactionType = LookupCodes.DebtTransactionType.Charge,
                            Amount          = amountToCharge,
                            BalanceAfter    = partyDebt.CurrentBalance,
                            RefType         = "OUTBOUND_ORDER",
                            RefId           = order.Id,
                            TransactionDate = now,
                            DueDate         = dto.DueDate!.Value.Date,
                            Note            = $"Công nợ phát sinh từ phiếu xuất {order.Id} (Đơn bán {salesOrder.SOCode})",
                            CreatedDate     = now,
                            CreatedBy       = userId
                        });

                        // #7: Ghi nhận tiền cọc (deposit) đã thu của đơn bán như một khoản thanh toán,
                        // giảm công nợ phải thu — CHỈ MỘT LẦN cho mỗi đơn bán.
                        //
                        // Cọc phải được GẮN ĐÚNG vào chứng từ công nợ của phiếu xuất này
                        // (RefType=OUTBOUND_ORDER, RefId=order.Id) — TRÙNG với giao dịch CHARGE ở trên.
                        // Nếu ghi RefType="SALES_ORDER_DEPOSIT"/RefId=SalesOrderId (không khớp bất kỳ
                        // chứng từ nào), thuật toán đối soát công nợ sẽ không match được và rơi xuống
                        // phân bổ FIFO theo hạn thanh toán → tiền cọc bị "chảy" nhầm sang chứng từ khác
                        // của cùng khách hàng (cọc hiển thị nhiều hơn thực tế). Dedup theo
                        // DeduplicationKey để vẫn chỉ ghi 1 lần cho mỗi đơn bán dù đơn được tách
                        // thành nhiều phiếu xuất.
                        if (salesOrder.DepositAmount.HasValue && salesOrder.DepositAmount.Value > 0)
                        {
                            var depositKey = $"SALES_ORDER_DEPOSIT-{salesOrder.Id}";
                            var depositRecorded = await _debtTransactionRepository.FirstOrDefaultAsync(x =>
                                x.DeduplicationKey == depositKey && !x.IsDeleted);

                            if (depositRecorded == null)
                            {
                                var depositAmount = salesOrder.DepositAmount.Value;
                                partyDebt.CurrentBalance  -= depositAmount;
                                partyDebt.LastModifiedDate  = now;
                                partyDebt.UpdatedBy         = userId;
                                await _partyDebtRepository.UpdateAsync(partyDebt);

                                await _debtTransactionRepository.CreateAsync(new DebtTransaction
                                {
                                    PartyDebtId       = partyDebt.Id,
                                    TransactionType   = LookupCodes.DebtTransactionType.Payment,
                                    Amount            = depositAmount,
                                    BalanceAfter      = partyDebt.CurrentBalance,
                                    RefType           = "OUTBOUND_ORDER",
                                    RefId             = order.Id,
                                    TransactionDate   = now,
                                    DeduplicationKey  = depositKey,
                                    Note              = $"Ghi nhận tiền cọc đã thu của đơn bán {salesOrder.SOCode}",
                                    CreatedDate       = now,
                                    CreatedBy         = userId
                                });
                            }
                        }
                    }
                }
            }

            // Ghi toàn bộ thay đổi vào DB rồi commit trong 1 transaction
            await _outboundOrderRepository.SaveChangesAsync();
            await ReleaseOutboundColumnLocksAsync(order, now, userId);
            await tx.CommitAsync();

            // Enqueue targeted background job evaluation for JOB-04 after transaction completes
            if (_scheduledJobService != null && partyDebt != null)
            {
                _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(partyDebt.Id, CancellationToken.None));
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return ApiResponse.Conflict(
                "Tồn kho đã thay đổi trong lúc xử lý. Vui lòng tải lại và thử lại.",
                ApiCodeConstants.OutboundOrder.ConcurrencyConflict);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse.UnprocessableEntity(ex.Message, ApiCodeConstants.OutboundOrder.InvalidRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }

        // Thông báo + push FCM sau khi đã commit thành công: gửi cho người tạo phiếu và các vai trò quản lý.
        var soCode = order.SalesOrder?.SOCode ?? order.Id.ToString();
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.OutboundDispatched,
            new NotificationTarget
            {
                UserIds = order.CreatedBy.HasValue ? new List<int> { order.CreatedBy.Value } : new List<int>(),
                RoleIds = new List<int>
                {
                    CommonConstants.Role.ADMIN,
                    CommonConstants.Role.OWNER,
                    CommonConstants.Role.SALES,
                },
            },
            new object[] { soCode },
            "/admin/outbound-orders",
            userId);

        return ApiResponse.Success(
            new { TotalDispatchedValue = totalDispatchedValue },
            "Xác nhận xuất kho thành công.");
    }

    private async Task<List<AllocateItemLotDto>> BuildRequestedBagAllocationsAsync(
        int productVariantId,
        int warehouseId,
        IReadOnlyCollection<AllocateItemLotDto> requestedLots)
    {
        if (_bagRepository == null || _bagContentRepository == null || requestedLots.Count == 0) return new();

        var requestedByInventory = requestedLots
            .GroupBy(x => x.InventoryId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityAllocated));
        var requestedInventoryIds = requestedByInventory.Keys.ToList();
        var inventories = await _inventoryRepository.FindByCondition(x =>
                requestedInventoryIds.Contains(x.Id) && !x.IsDeleted,
                false, x => x.PaddyLot)
            .ToListAsync();
        if (inventories.Count != requestedInventoryIds.Count)
            throw new InvalidOperationException("Có lô/vị trí được chọn không còn tồn tại.");
        if (inventories.Any(x => x.ProductVariantId != productVariantId || x.WarehouseId != warehouseId || !x.LocationId.HasValue || !x.PaddyLotId.HasValue))
            throw new InvalidOperationException("Lô/vị trí được chọn không khớp sản phẩm hoặc kho xuất.");

        var inventoryByLotLocation = inventories.ToDictionary(
            x => (LotId: x.PaddyLotId!.Value, LocationId: x.LocationId!.Value), x => x);
        var selectedLocationIds = inventories.Select(x => x.LocationId!.Value).Distinct().ToList();

        var bags = await _bagRepository.FindByCondition(x => x.LocationId.HasValue && x.Status == PaddyLotBagStatuses.Stored && x.WeightKg > 0 && !x.IsDeleted, false)
            .Include(x => x.Lot)
            .Include(x => x.Contents).ThenInclude(x => x.Lot).ThenInclude(x => x.Status)
            // Load complete physical columns. Bags of another SKU/lot are real blockers
            // and must not be skipped by the picking simulation.
            .Where(x => x.LocationId.HasValue && selectedLocationIds.Contains(x.LocationId.Value))
            .OrderBy(x => x.LocationId)
            .ThenByDescending(x => x.StackOrder)
            .ToListAsync();
        if (bags.Count == 0) return new();

        foreach (var group in bags.GroupBy(x => x.LocationId))
        {
            var topOrder = group.Max(x => x.StackOrder);
            if (group.Any(x => !x.IsFull && x.StackOrder != topOrder))
                throw new InvalidOperationException($"Bao mở tại vị trí {group.Key} không nằm trên đỉnh cột.");
            if (group.Count(x => !x.IsFull) > 1)
                throw new InvalidOperationException($"Vị trí {group.Key} có nhiều hơn một bao mở của sản phẩm.");
            if (group.SelectMany(x => x.Contents).Any(x => x.WeightKg > 0 && !x.IsDeleted &&
                    (x.Lot.Status?.Code == LotStatusCodeConstants.Quarantine || x.Lot.Status?.IsSellable == false)))
                throw new InvalidOperationException(
                    $"Vị trí {group.Key} có bao hỗn hợp chứa thành phần lô đang cách ly hoặc không được phép bán.");
        }

        var allocations = new List<AllocateItemLotDto>();
        var remaining = requestedByInventory.ToDictionary(x => x.Key, x => x.Value);
        var selectedFullBagIds = new HashSet<int>();

        // A request line that is exactly one standard bag represents an explicit
        // "full bag" choice from the UI. Preserve that choice before simulating
        // partial picks from the top of the stack. Without this, a 10 kg full-bag
        // request could be silently rewritten as 1 kg from an open bag + 9 kg from
        // a new split bag when both belong to the same inventory row.
        foreach (var request in requestedLots)
        {
            if (!inventories.Any(x => x.Id == request.InventoryId) ||
                remaining.GetValueOrDefault(request.InventoryId) + 0.0005m < request.QuantityAllocated)
                continue;

            var inventory = inventories.First(x => x.Id == request.InventoryId);
            var locationBags = bags
                .Where(x => x.LocationId == inventory.LocationId)
                .OrderByDescending(x => x.StackOrder)
                .ThenByDescending(x => x.Id)
                .ToList();

            var fullBag = locationBags.FirstOrDefault(candidate =>
            {
                if (selectedFullBagIds.Contains(candidate.Id) || !candidate.IsFull ||
                    !candidate.StandardWeightKg.HasValue ||
                    Math.Abs(request.QuantityAllocated - candidate.StandardWeightKg.Value) > 0.0005m ||
                    Math.Abs(request.QuantityAllocated - candidate.WeightKg) > 0.0005m)
                    return false;

                var activeContents = candidate.Contents
                    .Where(x => !x.IsDeleted && x.WeightKg > 0.0005m)
                    .ToList();
                if (activeContents.Count == 0 || activeContents.Any(x => x.LotId != inventory.PaddyLotId))
                    return false;

                // Skipping an open bag of the same lot is allowed for an explicit
                // full-bag choice. A different lot above remains a real blocker.
                return locationBags
                    .Where(x => x.StackOrder > candidate.StackOrder && !selectedFullBagIds.Contains(x.Id))
                    .All(x => x.Contents
                        .Where(c => !c.IsDeleted && c.WeightKg > 0.0005m)
                        .All(c => c.LotId == inventory.PaddyLotId));
            });

            if (fullBag == null) continue;

            allocations.Add(new AllocateItemLotDto
            {
                InventoryId = request.InventoryId,
                QuantityAllocated = request.QuantityAllocated
            });
            selectedFullBagIds.Add(fullBag.Id);
            remaining[request.InventoryId] -= request.QuantityAllocated;
        }

        foreach (var locationGroup in bags.GroupBy(x => x.LocationId!.Value))
        {
            var locationDemandIds = inventories.Where(x => x.LocationId == locationGroup.Key).Select(x => x.Id).ToHashSet();

            foreach (var bag in locationGroup.OrderByDescending(x => x.StackOrder))
            {
                if (locationDemandIds.All(id => remaining.GetValueOrDefault(id) <= 0.0005m)) break;
                if (selectedFullBagIds.Contains(bag.Id)) continue;

                var activeContents = bag.Contents.Where(x => x.WeightKg > 0 && !x.IsDeleted).OrderByDescending(x => x.Id).ToList();
                foreach (var content in activeContents)
                {
                    if (locationDemandIds.All(id => remaining.GetValueOrDefault(id) <= 0.0005m)) break;

                    if (!inventoryByLotLocation.TryGetValue((content.LotId, locationGroup.Key), out var inventory) ||
                        remaining.GetValueOrDefault(inventory.Id) <= 0.0005m)
                    {
                        throw new InvalidOperationException(
                            $"Không thể lấy lô đã chọn tại vị trí #{locationGroup.Key}: bao #{bag.BagNo} của lô khác đang chặn phía trên. " +
                            "Vui lòng chọn lô đang ở đỉnh cột hoặc thực hiện đảo bao trước.");
                    }

                    var take = Math.Min(remaining[inventory.Id], content.WeightKg);
                    if (take > 0)
                    {
                        allocations.Add(new AllocateItemLotDto
                        {
                            InventoryId = inventory.Id,
                            QuantityAllocated = take
                        });
                        remaining[inventory.Id] -= take;
                    }
                    if (remaining[inventory.Id] <= 0.0005m && take + 0.0005m < content.WeightKg &&
                        locationDemandIds.Any(id => remaining.GetValueOrDefault(id) > 0.0005m))
                    {
                        throw new InvalidOperationException(
                            $"Bao #{bag.BagNo} vẫn còn hàng và đang chặn lô tiếp theo tại vị trí #{locationGroup.Key}.");
                    }
                }
            }
        }

        var missing = remaining.Where(x => x.Value > 0.0005m).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Không thể lấy đủ {missing.Sum(x => x.Value):0.###} kg từ các lô đã chọn theo thứ tự bao vật lý hiện tại.");

        return allocations;
    }

    private async Task<HashSet<int>> StagePhysicalBagsAsync(
        OutboundOrder order, Location staging, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagContentRepository == null || _bagMovementRepository == null)
            throw new InvalidOperationException("Chưa cấu hình đầy đủ lớp bao vật lý cho nghiệp vụ đóng gói.");

        var allocations = order.OutboundOrderItems
            .Where(i => !i.IsDeleted)
            .SelectMany(ActiveAllocations)
            .Where(a => a.PaddyLotId.HasValue && a.QuantityPicked > 0.0005m)
            .OrderBy(a => a.Id)
            .ToList();
        var partiallySplitLocationIds = new HashSet<int>();
        var stagingStack = await _bagRepository.FindByCondition(x =>
                x.LocationId == staging.Id && x.Status == PaddyLotBagStatuses.OutboundStaging && !x.IsDeleted)
            .MaxAsync(x => (int?)x.StackOrder) ?? 0;

        foreach (var locationGroup in allocations.GroupBy(a => a.LocationId))
        {
            var remaining = locationGroup.ToDictionary(a => a.Id, a => a.QuantityPicked);
            var allocationsById = locationGroup.ToDictionary(a => a.Id);
            var bags = await _bagRepository.FindByCondition(x =>
                    x.LocationId == locationGroup.Key && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, true)
                .Include(x => x.Location)
                .Include(x => x.Lot)
                .Include(x => x.Contents).ThenInclude(x => x.Lot)
                .OrderByDescending(x => x.StackOrder)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            var preferredFullBagByAllocationId = new Dictionary<int, PaddyLotBag>();
            var claimedFullBagIds = new HashSet<int>();
            foreach (var allocation in locationGroup.OrderBy(x => x.Id))
            {
                var fullBag = bags.FirstOrDefault(candidate =>
                    !claimedFullBagIds.Contains(candidate.Id) &&
                    candidate.IsFull &&
                    candidate.StandardWeightKg.HasValue &&
                    Math.Abs(allocation.QuantityPicked - candidate.StandardWeightKg.Value) <= 0.0005m &&
                    Math.Abs(allocation.QuantityPicked - candidate.WeightKg) <= 0.0005m &&
                    candidate.Contents.Any(x => !x.IsDeleted && x.WeightKg > 0.0005m) &&
                    candidate.Contents
                        .Where(x => !x.IsDeleted && x.WeightKg > 0.0005m)
                        .All(x => x.LotId == allocation.PaddyLotId) &&
                    bags
                        .Where(x => x.StackOrder > candidate.StackOrder && !claimedFullBagIds.Contains(x.Id))
                        .All(x => x.Contents
                            .Where(c => !c.IsDeleted && c.WeightKg > 0.0005m)
                            .All(c => c.LotId == allocation.PaddyLotId)));
                if (fullBag == null) continue;

                preferredFullBagByAllocationId[allocation.Id] = fullBag;
                claimedFullBagIds.Add(fullBag.Id);
            }

            var preferredAllocationByBagId = preferredFullBagByAllocationId
                .ToDictionary(x => x.Value.Id, x => x.Key);
            var preferredBagIds = preferredAllocationByBagId.Keys.ToHashSet();
            var bagsToProcess = locationGroup
                .OrderBy(x => x.Id)
                .Where(x => preferredFullBagByAllocationId.ContainsKey(x.Id))
                .Select(x => preferredFullBagByAllocationId[x.Id])
                .Concat(bags.Where(x => !preferredBagIds.Contains(x.Id)))
                .ToList();

            foreach (var bag in bagsToProcess)
            {
                if (remaining.Values.All(x => x <= 0.0005m)) break;

                var selections = new List<(OutboundOrderItemAllocation Allocation, PaddyLotBagContent Content, decimal Weight)>();
                foreach (var content in bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0.0005m).OrderBy(x => x.Id))
                {
                    var contentRemaining = content.WeightKg;
                    foreach (var allocation in locationGroup.Where(a =>
                                 a.PaddyLotId == content.LotId && remaining[a.Id] > 0.0005m &&
                                 (preferredAllocationByBagId.TryGetValue(bag.Id, out var preferredAllocationId)
                                     ? a.Id == preferredAllocationId
                                     : !preferredFullBagByAllocationId.ContainsKey(a.Id)))
                             .OrderBy(a => a.Id))
                    {
                        var take = Math.Min(contentRemaining, remaining[allocation.Id]);
                        if (take <= 0.0005m) continue;
                        selections.Add((allocation, content, take));
                        remaining[allocation.Id] -= take;
                        contentRemaining -= take;
                        if (contentRemaining <= 0.0005m) break;
                    }
                }

                var selectedWeight = selections.Sum(x => x.Weight);
                if (selectedWeight <= 0.0005m)
                {
                    var wantedLots = remaining.Where(x => x.Value > 0.0005m)
                        .Select(x => allocationsById[x.Key].PaddyLot?.LotCode ?? $"#{allocationsById[x.Key].PaddyLotId}");
                    throw new InvalidOperationException(
                        $"Bao #{bag.BagNo} đang chặn các lô cần lấy ({string.Join(", ", wantedLots.Distinct())}) tại cột {FormatLocationCode(bag.Location)}.");
                }

                PaddyLotBag stagedBag;
                if (Math.Abs(selectedWeight - bag.WeightKg) <= 0.0005m)
                {
                    stagedBag = bag;
                    stagedBag.LocationId = staging.Id;
                    stagedBag.Status = PaddyLotBagStatuses.OutboundStaging;
                    stagedBag.StackOrder = ++stagingStack;
                    stagedBag.OpenBagKey = null;
                    stagedBag.LastModifiedDate = now;
                    stagedBag.UpdatedBy = userId;
                    await _bagRepository.UpdateAsync(stagedBag);
                }
                else
                {
                    var representativeLotId = selections
                        .GroupBy(x => x.Content.LotId)
                        .OrderByDescending(x => x.Sum(y => y.Weight))
                        .ThenBy(x => x.Key)
                        .First().Key;
                    var nextBagNo = (await _bagRepository.FindByCondition(x =>
                            x.LotId == representativeLotId && !x.IsDeleted)
                        .MaxAsync(x => (int?)x.BagNo) ?? 0) + 1;
                    stagedBag = new PaddyLotBag
                    {
                        LotId = representativeLotId,
                        BagNo = nextBagNo,
                        WeightKg = selectedWeight,
                        LocationId = staging.Id,
                        Status = PaddyLotBagStatuses.OutboundStaging,
                        StackOrder = ++stagingStack,
                        StandardWeightKg = bag.StandardWeightKg,
                        IsFull = bag.StandardWeightKg.HasValue && selectedWeight + 0.0005m >= bag.StandardWeightKg.Value,
                        BagKind = bag.BagKind,
                        SourceBagId = bag.Id,
                        QrCode = $"PLB-{Guid.NewGuid():N}".ToUpperInvariant(),
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _bagRepository.CreateAsync(stagedBag);
                    await _bagRepository.SaveChangesAsync();

                    foreach (var lotSelection in selections.GroupBy(x => x.Content.LotId))
                    {
                        await _bagContentRepository.CreateAsync(new PaddyLotBagContent
                        {
                            BagId = stagedBag.Id,
                            LotId = lotSelection.Key,
                            WeightKg = lotSelection.Sum(x => x.Weight),
                            CreatedBy = userId,
                            CreatedDate = now
                        });
                    }

                    foreach (var contentSelection in selections.GroupBy(x => x.Content))
                    {
                        var content = contentSelection.Key;
                        content.WeightKg -= contentSelection.Sum(x => x.Weight);
                        content.LastModifiedDate = now;
                        content.UpdatedBy = userId;
                        await _bagContentRepository.UpdateAsync(content);
                    }
                    bag.WeightKg -= selectedWeight;
                    if (bag.StandardWeightKg.HasValue)
                    {
                        bag.IsFull = bag.WeightKg + 0.0005m >= bag.StandardWeightKg.Value;
                        bag.OpenBagKey = bag.IsFull ? null : $"{bag.Lot.ProductVariantId}:{bag.Lot.WarehouseId}";
                    }
                    RefreshRepresentativeLot(bag);
                    bag.LastModifiedDate = now;
                    bag.UpdatedBy = userId;
                    await _bagRepository.UpdateAsync(bag);
                    partiallySplitLocationIds.Add(locationGroup.Key);
                }

                decimal stagedSoFar = 0;
                foreach (var allocationSelection in selections.GroupBy(x => x.Allocation))
                {
                    var moved = allocationSelection.Sum(x => x.Weight);
                    await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                    {
                        BagId = stagedBag.Id,
                        MovementType = PaddyLotBagMovementTypes.OutboundStage,
                        FromLocationId = locationGroup.Key,
                        ToLocationId = staging.Id,
                        WeightKg = moved,
                        BeforeWeightKg = stagedSoFar,
                        AfterWeightKg = stagedSoFar + moved,
                        ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                        ReferenceId = order.Id,
                        ReferenceItemId = allocationSelection.Key.Id,
                        Note = $"Chuyển {moved:0.###} kg sang khu chờ xuất",
                        CreatedBy = userId,
                        CreatedDate = now
                    });
                    stagedSoFar += moved;
                }
            }

            var missing = remaining.Where(x => x.Value > 0.0005m).Sum(x => x.Value);
            if (missing > 0.0005m)
                throw new InvalidOperationException(
                    $"Không thể chuyển đủ {missing:0.###} kg từ cột #{locationGroup.Key} sang khu chờ xuất.");
        }

        await _bagContentRepository.SaveChangesAsync();
        return partiallySplitLocationIds;
    }

    private async Task TransferPackedInventoryToStagingAsync(
        OutboundOrder order, Location staging, int userId, DateTime now)
    {
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            foreach (var allocation in ActiveAllocations(item))
            {
                if (allocation.QuantityPicked <= 0.0005m) continue;
                var source = allocation.Inventory;
                if (source == null || source.IsDeleted || !allocation.PaddyLotId.HasValue)
                    throw new InvalidOperationException($"Không tìm thấy tồn nguồn cho allocation #{allocation.Id}.");

                var stagingInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    source.ProductVariantId, source.WarehouseId, staging.Id, allocation.PaddyLotId);
                if (stagingInventory == null)
                {
                    stagingInventory = new Inventory
                    {
                        WarehouseId = source.WarehouseId,
                        LocationId = staging.Id,
                        ProductVariantId = source.ProductVariantId,
                        PaddyLotId = allocation.PaddyLotId,
                        CostPrice = source.CostPrice,
                        QuantityOnHand = 0,
                        QuantityReserved = 0,
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _inventoryRepository.CreateAsync(stagingInventory);
                    await _inventoryRepository.SaveChangesAsync();
                }

                var sourceBefore = source.QuantityOnHand;
                source.QuantityOnHand -= allocation.QuantityPicked;
                source.QuantityReserved -= allocation.QuantityAllocated;
                if (source.QuantityOnHand < -0.0005m || source.QuantityReserved < -0.0005m)
                    throw new InvalidOperationException($"Tồn nguồn của allocation #{allocation.Id} không đủ để chuyển sang khu chờ xuất.");
                source.QuantityOnHand = Math.Max(0, source.QuantityOnHand);
                source.QuantityReserved = Math.Max(0, source.QuantityReserved);
                source.LastModifiedDate = now;
                source.UpdatedBy = userId;
                await _inventoryRepository.UpdateAsync(source);

                var stagingBefore = stagingInventory.QuantityOnHand;
                if (stagingBefore > 0)
                    stagingInventory.CostPrice = Math.Round(
                        ((stagingBefore * stagingInventory.CostPrice) + (allocation.QuantityPicked * source.CostPrice)) /
                        (stagingBefore + allocation.QuantityPicked), 2);
                else
                    stagingInventory.CostPrice = source.CostPrice;
                stagingInventory.QuantityOnHand += allocation.QuantityPicked;
                stagingInventory.QuantityReserved += allocation.QuantityAllocated;
                stagingInventory.LastModifiedDate = now;
                stagingInventory.UpdatedBy = userId;
                await _inventoryRepository.UpdateAsync(stagingInventory);

                await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(new InventoryTransaction
                {
                    InventoryId = source.Id,
                    WarehouseId = source.WarehouseId,
                    LocationId = source.LocationId,
                    ProductVariantId = source.ProductVariantId,
                    PaddyLotId = source.PaddyLotId,
                    TransactionType = InventoryTransactionTypeConstants.TransferOut,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                    ReferenceId = order.Id,
                    ReferenceItemId = allocation.Id,
                    Quantity = -allocation.QuantityPicked,
                    BeforeQuantity = sourceBefore,
                    AfterQuantity = source.QuantityOnHand,
                    WeightKg = -allocation.QuantityPicked,
                    Note = "Chuyển hàng đã đóng gói sang khu chờ xuất",
                    CreatedBy = userId,
                    CreatedDate = now
                });
                await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(new InventoryTransaction
                {
                    InventoryId = stagingInventory.Id,
                    WarehouseId = stagingInventory.WarehouseId,
                    LocationId = staging.Id,
                    ProductVariantId = stagingInventory.ProductVariantId,
                    PaddyLotId = stagingInventory.PaddyLotId,
                    TransactionType = InventoryTransactionTypeConstants.TransferIn,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                    ReferenceId = order.Id,
                    ReferenceItemId = allocation.Id,
                    Quantity = allocation.QuantityPicked,
                    BeforeQuantity = stagingBefore,
                    AfterQuantity = stagingInventory.QuantityOnHand,
                    WeightKg = allocation.QuantityPicked,
                    Note = "Nhập hàng đã đóng gói vào khu chờ xuất",
                    CreatedBy = userId,
                    CreatedDate = now
                });

                if (allocation.Location != null)
                {
                    allocation.Location.CurrentOccupancy = Math.Max(0, allocation.Location.CurrentOccupancy - allocation.QuantityPicked);
                    if (allocation.Location.CurrentOccupancy <= 0.0005m)
                        allocation.Location.CurrentProductVariantId = null;
                    allocation.Location.LastModifiedDate = now;
                    allocation.Location.UpdatedBy = userId;
                    if (_locationRepository != null)
                        await _locationRepository.UpdateAsync(allocation.Location);
                }
                staging.CurrentOccupancy += allocation.QuantityPicked;
            }
        }

        staging.LastModifiedDate = now;
        staging.UpdatedBy = userId;
        if (_locationRepository != null)
            await _locationRepository.UpdateAsync(staging);
    }

    private async Task<HashSet<int>> ConsumeStagedBagsAsync(
        int outboundOrderId, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagContentRepository == null || _bagMovementRepository == null)
            return new HashSet<int>();

        var stagedMovements = await _bagMovementRepository.FindByCondition(x =>
                x.ReferenceType == InventoryReferenceTypeConstants.OutboundOrder &&
                x.ReferenceId == outboundOrderId &&
                x.MovementType == PaddyLotBagMovementTypes.OutboundStage &&
                x.ReferenceItemId.HasValue && !x.IsDeleted, true)
            .ToListAsync();
        if (stagedMovements.Count == 0) return new HashSet<int>();

        var bagIds = stagedMovements.Select(x => x.BagId).Distinct().ToList();
        var bags = await _bagRepository.FindByCondition(x =>
                bagIds.Contains(x.Id) && x.Status == PaddyLotBagStatuses.OutboundStaging && !x.IsDeleted, true)
            .Include(x => x.Contents)
            .ToListAsync();
        if (bags.Count != bagIds.Count)
            throw new InvalidOperationException("Một hoặc nhiều bao ở khu chờ xuất không còn đúng trạng thái để dispatch.");

        foreach (var bag in bags)
        {
            var before = bag.WeightKg;
            var fromLocationId = bag.LocationId;
            foreach (var content in bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0))
            {
                content.WeightKg = 0;
                content.LastModifiedDate = now;
                content.UpdatedBy = userId;
                await _bagContentRepository.UpdateAsync(content);
            }
            bag.WeightKg = 0;
            bag.Status = PaddyLotBagStatuses.Consumed;
            bag.LocationId = null;
            bag.StackOrder = 0;
            bag.IsFull = false;
            bag.OpenBagKey = null;
            bag.LastModifiedDate = now;
            bag.UpdatedBy = userId;
            await _bagRepository.UpdateAsync(bag);

            decimal consumedSoFar = 0;
            foreach (var stagedMovement in stagedMovements.Where(x => x.BagId == bag.Id).OrderBy(x => x.Id))
            {
                var moved = Math.Min(stagedMovement.WeightKg, before - consumedSoFar);
                if (moved <= 0.0005m) continue;
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = bag.Id,
                    MovementType = PaddyLotBagMovementTypes.OutboundConsume,
                    FromLocationId = fromLocationId,
                    ToLocationId = null,
                    WeightKg = moved,
                    BeforeWeightKg = before - consumedSoFar,
                    AfterWeightKg = Math.Max(0, before - consumedSoFar - moved),
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                    ReferenceId = outboundOrderId,
                    ReferenceItemId = stagedMovement.ReferenceItemId,
                    Note = "Xuất hàng từ khu chờ xuất",
                    CreatedBy = userId,
                    CreatedDate = now
                });
                consumedSoFar += moved;
            }
        }
        await _bagContentRepository.SaveChangesAsync();
        return stagedMovements.Select(x => x.ReferenceItemId!.Value).ToHashSet();
    }

    private async Task<bool> ReturnStagedBagsAsync(
        OutboundOrder order, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagMovementRepository == null || _locationRepository == null)
            return false;

        var movements = await _bagMovementRepository.FindByCondition(x =>
                x.ReferenceType == InventoryReferenceTypeConstants.OutboundOrder &&
                x.ReferenceId == order.Id &&
                x.MovementType == PaddyLotBagMovementTypes.OutboundStage &&
                x.ReferenceItemId.HasValue && !x.IsDeleted, true)
            .ToListAsync();
        if (movements.Count == 0) return false;

        var bagIds = movements.Select(x => x.BagId).Distinct().ToList();
        var bags = await _bagRepository.FindByCondition(x =>
                bagIds.Contains(x.Id) && x.Status == PaddyLotBagStatuses.OutboundStaging && !x.IsDeleted, true)
            .Include(x => x.Lot)
            .Include(x => x.Contents)
            .ToListAsync();
        if (bags.Count != bagIds.Count)
            throw new InvalidOperationException("Một hoặc nhiều bao chờ xuất không còn nguyên trạng để hoàn phiếu.");

        var sourceLocationIds = movements.Where(m => m.FromLocationId.HasValue)
            .Select(m => m.FromLocationId!.Value).Distinct().ToList();
        var locations = await _locationRepository.FindByCondition(x =>
                sourceLocationIds.Contains(x.Id) && !x.IsDeleted, true)
            .ToDictionaryAsync(x => x.Id);
        var stagingId = movements.Select(x => x.ToLocationId).FirstOrDefault(x => x.HasValue);
        var staging = stagingId.HasValue
            ? await _locationRepository.GetByIdAsync(stagingId.Value)
            : null;

        foreach (var bag in bags.OrderBy(x => x.Id))
        {
            var fromIds = movements.Where(x => x.BagId == bag.Id && x.FromLocationId.HasValue)
                .Select(x => x.FromLocationId!.Value).Distinct().ToList();
            if (fromIds.Count != 1 || !locations.TryGetValue(fromIds[0], out var destination))
                throw new InvalidOperationException($"Không xác định được cột hoàn về cho bao #{bag.BagNo}.");
            if (!destination.IsActive || destination.IsQuarantine || destination.IsOutboundStaging ||
                (destination.OutboundLockOrderId.HasValue && destination.OutboundLockOrderId != order.Id))
                throw new InvalidOperationException($"Cột '{FormatLocationCode(destination)}' hiện không thể nhận lại bao #{bag.BagNo}.");
            if (destination.MaxCapacity.HasValue && destination.CurrentOccupancy + bag.WeightKg > destination.MaxCapacity.Value + 0.0005m)
                throw new InvalidOperationException($"Cột '{FormatLocationCode(destination)}' không đủ sức chứa để hoàn bao #{bag.BagNo}.");
            if (destination.CurrentProductVariantId.HasValue && destination.CurrentProductVariantId != bag.Lot.ProductVariantId)
                throw new InvalidOperationException($"Cột '{FormatLocationCode(destination)}' đang chứa sản phẩm khác, không thể hoàn bao #{bag.BagNo}.");

            if (bag.SourceBagId.HasValue && _bagContentRepository != null)
            {
                var sourceBag = await _bagRepository.FirstOrDefaultAsync(x =>
                    x.Id == bag.SourceBagId.Value && x.LocationId == destination.Id &&
                    x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, true,
                    x => x.Lot, x => x.Contents);
                if (sourceBag == null)
                    throw new InvalidOperationException($"Không tìm thấy bao nguồn của bao tách #{bag.BagNo} để hoàn phiếu.");
                var returnedWeight = bag.WeightKg;
                var sourceBefore = sourceBag.WeightKg;
                foreach (var content in bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0))
                {
                    var sourceContent = sourceBag.Contents.FirstOrDefault(x =>
                        !x.IsDeleted && x.LotId == content.LotId);
                    if (sourceContent == null)
                    {
                        sourceContent = new PaddyLotBagContent
                        {
                            BagId = sourceBag.Id, LotId = content.LotId, WeightKg = 0,
                            CreatedBy = userId, CreatedDate = now
                        };
                        sourceBag.Contents.Add(sourceContent);
                        await _bagContentRepository.CreateAsync(sourceContent);
                    }
                    sourceContent.WeightKg += content.WeightKg;
                    sourceContent.LastModifiedDate = now;
                    sourceContent.UpdatedBy = userId;
                    await _bagContentRepository.UpdateAsync(sourceContent);
                    content.WeightKg = 0;
                    content.LastModifiedDate = now;
                    content.UpdatedBy = userId;
                    await _bagContentRepository.UpdateAsync(content);
                }
                sourceBag.WeightKg += returnedWeight;
                sourceBag.IsFull = !sourceBag.StandardWeightKg.HasValue ||
                    sourceBag.WeightKg + 0.0005m >= sourceBag.StandardWeightKg.Value;
                sourceBag.OpenBagKey = sourceBag.IsFull ? null : $"{sourceBag.Lot.ProductVariantId}:{sourceBag.Lot.WarehouseId}";
                RefreshRepresentativeLot(sourceBag);
                sourceBag.LastModifiedDate = now;
                sourceBag.UpdatedBy = userId;
                await _bagRepository.UpdateAsync(sourceBag);
                bag.WeightKg = 0;
                bag.LocationId = null;
                bag.Status = PaddyLotBagStatuses.Reversed;
                bag.StackOrder = 0;
                bag.IsFull = false;
                bag.OpenBagKey = null;
                bag.LastModifiedDate = now;
                bag.UpdatedBy = userId;
                await _bagRepository.UpdateAsync(bag);
                destination.CurrentOccupancy += returnedWeight;
                destination.CurrentProductVariantId ??= sourceBag.Lot.ProductVariantId;
                await _locationRepository.UpdateAsync(destination);
                if (staging != null) staging.CurrentOccupancy = Math.Max(0, staging.CurrentOccupancy - returnedWeight);
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = sourceBag.Id, MovementType = PaddyLotBagMovementTypes.OutboundStageReturn,
                    FromLocationId = stagingId, ToLocationId = destination.Id, WeightKg = returnedWeight,
                    BeforeWeightKg = sourceBefore, AfterWeightKg = sourceBag.WeightKg,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder, ReferenceId = order.Id,
                    ReferenceItemId = movements.First(x => x.BagId == bag.Id).ReferenceItemId,
                    Note = $"Ghép lại vào bao nguồn #{sourceBag.BagNo} do hủy phiếu",
                    CreatedBy = userId, CreatedDate = now
                });
                continue;
            }

            var stack = await _bagRepository.FindByCondition(x =>
                    x.LocationId == destination.Id && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                .MaxAsync(x => (int?)x.StackOrder) ?? 0;
            bag.LocationId = destination.Id;
            bag.Status = PaddyLotBagStatuses.Stored;
            bag.StackOrder = stack + 1;
            bag.OpenBagKey = bag.IsFull ? null : $"{bag.Lot.ProductVariantId}:{bag.Lot.WarehouseId}";
            bag.LastModifiedDate = now;
            bag.UpdatedBy = userId;
            await _bagRepository.UpdateAsync(bag);

            destination.CurrentOccupancy += bag.WeightKg;
            destination.CurrentProductVariantId ??= bag.Lot.ProductVariantId;
            destination.LastModifiedDate = now;
            destination.UpdatedBy = userId;
            await _locationRepository.UpdateAsync(destination);
            if (staging != null)
                staging.CurrentOccupancy = Math.Max(0, staging.CurrentOccupancy - bag.WeightKg);

            await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
            {
                BagId = bag.Id,
                MovementType = PaddyLotBagMovementTypes.OutboundStageReturn,
                FromLocationId = stagingId,
                ToLocationId = destination.Id,
                WeightKg = bag.WeightKg,
                BeforeWeightKg = bag.WeightKg,
                AfterWeightKg = bag.WeightKg,
                ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                ReferenceId = order.Id,
                ReferenceItemId = movements.First(x => x.BagId == bag.Id).ReferenceItemId,
                Note = "Hoàn nguyên bao từ khu chờ xuất do hủy phiếu",
                CreatedBy = userId,
                CreatedDate = now
            });
        }

        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            foreach (var allocation in ActiveAllocations(item))
            {
                if (allocation.QuantityPicked <= 0.0005m || !allocation.PaddyLotId.HasValue) continue;
                var source = allocation.Inventory;
                if (source == null || source.IsDeleted)
                    throw new InvalidOperationException($"Không tìm thấy tồn nguồn cho allocation #{allocation.Id}.");
                var stagingInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    source.ProductVariantId, source.WarehouseId, stagingId, allocation.PaddyLotId)
                    ?? throw new InvalidOperationException($"Không tìm thấy tồn staging cho allocation #{allocation.Id}.");

                var stagingBefore = stagingInventory.QuantityOnHand;
                stagingInventory.QuantityOnHand -= allocation.QuantityPicked;
                stagingInventory.QuantityReserved -= allocation.QuantityAllocated;
                if (stagingInventory.QuantityOnHand < -0.0005m || stagingInventory.QuantityReserved < -0.0005m)
                    throw new InvalidOperationException($"Tồn staging của allocation #{allocation.Id} không đủ để hoàn phiếu.");
                stagingInventory.QuantityOnHand = Math.Max(0, stagingInventory.QuantityOnHand);
                stagingInventory.QuantityReserved = Math.Max(0, stagingInventory.QuantityReserved);
                stagingInventory.LastModifiedDate = now;
                stagingInventory.UpdatedBy = userId;
                await _inventoryRepository.UpdateAsync(stagingInventory);

                var sourceBefore = source.QuantityOnHand;
                source.QuantityOnHand += allocation.QuantityPicked;
                source.LastModifiedDate = now;
                source.UpdatedBy = userId;
                await _inventoryRepository.UpdateAsync(source);

                await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(new InventoryTransaction
                {
                    InventoryId = stagingInventory.Id, WarehouseId = stagingInventory.WarehouseId,
                    LocationId = stagingInventory.LocationId, ProductVariantId = stagingInventory.ProductVariantId,
                    PaddyLotId = stagingInventory.PaddyLotId, TransactionType = InventoryTransactionTypeConstants.TransferOut,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder, ReferenceId = order.Id,
                    ReferenceItemId = allocation.Id, Quantity = -allocation.QuantityPicked,
                    BeforeQuantity = stagingBefore, AfterQuantity = stagingInventory.QuantityOnHand,
                    WeightKg = -allocation.QuantityPicked, Note = "Hoàn hàng khỏi khu chờ xuất do hủy phiếu",
                    CreatedBy = userId, CreatedDate = now
                });
                await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(new InventoryTransaction
                {
                    InventoryId = source.Id, WarehouseId = source.WarehouseId, LocationId = source.LocationId,
                    ProductVariantId = source.ProductVariantId, PaddyLotId = source.PaddyLotId,
                    TransactionType = InventoryTransactionTypeConstants.TransferIn,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder, ReferenceId = order.Id,
                    ReferenceItemId = allocation.Id, Quantity = allocation.QuantityPicked,
                    BeforeQuantity = sourceBefore, AfterQuantity = source.QuantityOnHand,
                    WeightKg = allocation.QuantityPicked, Note = "Nhập lại hàng do hủy phiếu xuất",
                    CreatedBy = userId, CreatedDate = now
                });
            }
        }

        if (staging != null)
        {
            staging.LastModifiedDate = now;
            staging.UpdatedBy = userId;
            await _locationRepository.UpdateAsync(staging);
        }
        await _bagRepository.SaveChangesAsync();
        return true;
    }

    private async Task ConsumePhysicalBagsAsync(int lotId, int locationId, decimal requestedKg,
        int outboundOrderId, int allocationId, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagContentRepository == null) return;
        var targetLot = await _paddyLotRepository.GetByIdAsync(lotId);
        if (targetLot == null) throw new InvalidOperationException($"Không tìm thấy lô #{lotId} của bao vật lý.");
        var targetLotCode = !string.IsNullOrWhiteSpace(targetLot.LotCode) ? targetLot.LotCode : $"lô #{lotId}";

        var bags = await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, true)
            .Include(x => x.Location)
            .Include(x => x.Lot)
            .Include(x => x.Contents).ThenInclude(x => x.Lot).ThenInclude(x => x.Status)
            // Phải lấy đúng thứ tự vật lý từ đỉnh cột xuống. Bao mở chỉ được ưu tiên
            // khi chính nó đang nằm trên đỉnh, không được lấy xuyên qua bao phía trên.
            .OrderByDescending(x => x.StackOrder)
            .ThenByDescending(x => x.Id).ToListAsync();

        var locationName = bags.FirstOrDefault()?.Location != null
            ? FormatLocationCode(bags.First().Location) ?? $"vị trí #{locationId}"
            : $"vị trí #{locationId}";

        if (bags.Count > 0 && bags.Any(x => !x.IsFull && x.StackOrder != bags.Max(b => b.StackOrder)))
            throw new InvalidOperationException(
                $"Bao mở tại vị trí '{locationName}' đang bị bao khác chặn phía trên. Vui lòng chuyển các bao cản hoặc chuyển bao mở sang cột hàng lẻ trước.");
        if (bags.SelectMany(x => x.Contents).Any(x => x.WeightKg > 0 && !x.IsDeleted &&
                (x.Lot.Status?.Code == LotStatusCodeConstants.Quarantine || x.Lot.Status?.IsSellable == false)))
            throw new InvalidOperationException(
                $"Vị trí '{locationName}' có bao hỗn hợp chứa thành phần lô đang cách ly hoặc không được phép bán.");
        var available = bags.Sum(x => x.Contents.Where(c => c.LotId == lotId && !c.IsDeleted).Sum(c => c.WeightKg));
        if (available + 0.0005m < requestedKg)
            throw new InvalidOperationException($"Lớp bao vật lý của lô '{targetLotCode}' tại vị trí '{locationName}' chỉ còn {available:0.###} kg, không đủ {requestedKg:0.###} kg.");

        var remaining = requestedKg;
        foreach (var bag in bags)
        {
            var beforeBagWeight = bag.WeightKg;
            var fromLocationId = bag.LocationId;
            var targetContents = bag.Contents.Where(x => x.LotId == lotId && x.WeightKg > 0 && !x.IsDeleted).OrderBy(x => x.Id).ToList();
            if (targetContents.Count == 0)
            {
                var topBagLotCode = !string.IsNullOrWhiteSpace(bag.Lot?.LotCode) ? bag.Lot.LotCode : $"lô #{bag.LotId}";
                throw new InvalidOperationException(
                    $"Không thể lấy lô '{targetLotCode}': bao #{bag.BagNo} (thuộc lô '{topBagLotCode}') đang chặn ở đỉnh cột '{locationName}'.");
            }
            foreach (var content in targetContents)
            {
                var take = Math.Min(remaining, content.WeightKg);
                content.WeightKg -= take; bag.WeightKg -= take; remaining -= take;
                content.UpdatedBy = userId; content.LastModifiedDate = now;
                await _bagContentRepository.UpdateAsync(content);
                if (remaining <= 0.0005m) break;
            }
            bag.WeightKg = Math.Max(0, bag.WeightKg);
            if (bag.WeightKg <= 0.0005m) { bag.WeightKg = 0; bag.Status = PaddyLotBagStatuses.Consumed; bag.LocationId = null; bag.IsFull = false; bag.OpenBagKey = null; }
            else if (bag.StandardWeightKg.HasValue) { bag.IsFull = bag.WeightKg >= bag.StandardWeightKg.Value; bag.OpenBagKey = bag.IsFull ? null : $"{targetLot.ProductVariantId}:{targetLot.WarehouseId}"; }
            RefreshRepresentativeLot(bag);
            bag.UpdatedBy = userId; bag.LastModifiedDate = now;
            await _bagRepository.UpdateAsync(bag);
            var consumedFromBag = beforeBagWeight - bag.WeightKg;
            if (consumedFromBag > 0.0005m && _bagMovementRepository != null)
            {
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = bag.Id,
                    MovementType = PaddyLotBagMovementTypes.OutboundConsume,
                    FromLocationId = fromLocationId,
                    ToLocationId = bag.LocationId,
                    WeightKg = consumedFromBag,
                    BeforeWeightKg = beforeBagWeight,
                    AfterWeightKg = bag.WeightKg,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                    ReferenceId = outboundOrderId,
                    ReferenceItemId = allocationId,
                    Note = $"Xuất {consumedFromBag:0.###} kg thành phần lô #{lotId}",
                    CreatedBy = userId,
                    CreatedDate = now
                });
            }
            if (remaining <= 0.0005m) break;
        }
        await _bagContentRepository.SaveChangesAsync();
    }

    private static void RefreshRepresentativeLot(PaddyLotBag bag)
    {
        if (bag.WeightKg <= 0) return;
        var active = bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0.0005m).ToList();
        if (active.Count == 0)
            throw new InvalidOperationException($"Bao #{bag.BagNo} còn khối lượng nhưng không còn thành phần lô.");
        if (active.Any(x => x.LotId == bag.LotId)) return;
        bag.LotId = active.OrderByDescending(x => x.WeightKg).ThenBy(x => x.Id).First().LotId;
    }

    public async Task<ApiResponse> CompleteDeliveryAsync(int id, CompleteDeliveryDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Dispatched)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái DISPATCHED để xác nhận giao hàng thành công. Trạng thái hiện tại: '{order.OutboundOrderStatus?.Name}'.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        if (dto.PaymentAmount < 0)
            return ApiResponse.UnprocessableEntity("Số tiền khách thanh toán thêm không được âm.");

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        PartyDebt? paidPartyDebt = null;
        decimal? remainingDebt = null;

        await using var tx = await _outboundOrderRepository.BeginTransactionAsync();
        try
        {
            if (dto.PaymentAmount > 0)
            {
                var charge = await _debtTransactionRepository.FirstOrDefaultAsync(x =>
                    x.RefType == "OUTBOUND_ORDER" &&
                    x.RefId == order.Id &&
                    x.TransactionType == LookupCodes.DebtTransactionType.Charge &&
                    !x.IsDeleted);

                if (charge == null)
                {
                    await tx.RollbackAsync();
                    return ApiResponse.UnprocessableEntity("Phiếu xuất này không có công nợ phải thu để ghi nhận thanh toán.");
                }

                paidPartyDebt = await _partyDebtRepository.GetByIdAsync(charge.PartyDebtId);
                if (paidPartyDebt == null || paidPartyDebt.IsDeleted || !paidPartyDebt.IsActive)
                {
                    await tx.RollbackAsync();
                    return ApiResponse.UnprocessableEntity("Sổ công nợ khách hàng không tồn tại hoặc đang bị khóa.");
                }

                var debtTransactions = await _debtTransactionRepository
                    .FindByCondition(x => x.PartyDebtId == paidPartyDebt.Id && !x.IsDeleted)
                    .ToListAsync();

                decimal outstandingAmount;
                if (_debtAgingService != null)
                {
                    outstandingAmount = _debtAgingService
                        .CalculateDebtDocuments(paidPartyDebt, debtTransactions)
                        .Where(x => x.RefType == "OUTBOUND_ORDER" && x.RefId == order.Id)
                        .OrderBy(x => x.TransactionDate)
                        .FirstOrDefault(x => x.OutstandingAmount > 0m)?
                        .OutstandingAmount ?? 0m;
                }
                else
                {
                    outstandingAmount = Math.Max(0m, charge.Amount - debtTransactions
                        .Where(x => x.RefType == "OUTBOUND_ORDER" &&
                                    x.RefId == order.Id &&
                                    x.TransactionType == LookupCodes.DebtTransactionType.Payment)
                        .Sum(x => x.Amount));
                }

                if (outstandingAmount <= 0)
                {
                    await tx.RollbackAsync();
                    return ApiResponse.UnprocessableEntity("Công nợ của phiếu xuất này đã được thanh toán hết.");
                }

                if (dto.PaymentAmount > outstandingAmount)
                {
                    await tx.RollbackAsync();
                    return ApiResponse.UnprocessableEntity(
                        $"Số tiền thanh toán ({dto.PaymentAmount:N0} VNĐ) vượt quá số còn nợ của phiếu xuất ({outstandingAmount:N0} VNĐ).");
                }

                if (dto.PaymentAmount > paidPartyDebt.CurrentBalance)
                {
                    await tx.RollbackAsync();
                    return ApiResponse.UnprocessableEntity("Số tiền thanh toán vượt quá tổng dư nợ hiện tại của khách hàng.");
                }

                paidPartyDebt.CurrentBalance -= dto.PaymentAmount;
                paidPartyDebt.LastModifiedDate = now;
                paidPartyDebt.UpdatedBy = userId;
                await _partyDebtRepository.UpdateAsync(paidPartyDebt);

                await _debtTransactionRepository.CreateAsync(new DebtTransaction
                {
                    PartyDebtId = paidPartyDebt.Id,
                    TransactionType = LookupCodes.DebtTransactionType.Payment,
                    Amount = dto.PaymentAmount,
                    BalanceAfter = paidPartyDebt.CurrentBalance,
                    RefType = "OUTBOUND_ORDER",
                    RefId = order.Id,
                    TransactionDate = now,
                    Note = $"Khách hàng thanh toán khi nhận hàng - phiếu xuất {order.Id}",
                    DeduplicationKey = $"DELIVERY_PAYMENT-{order.Id}",
                    CreatedDate = now,
                    CreatedBy = userId
                });

                remainingDebt = outstandingAmount - dto.PaymentAmount;
            }

            order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Completed);
            order.ReceiverName = dto.ReceiverName;
            order.DeliveryNote = dto.DeliveryNote;
            order.ProofImageUrl = dto.ProofImageUrl;
            order.CompletedDate = now;
            order.LastModifiedDate = now;
            order.UpdatedBy = userId;
            await _outboundOrderRepository.UpdateAsync(order);
            await _outboundOrderRepository.SaveChangesAsync();

            // Kiểm tra xem tất cả các OutboundOrder của SalesOrder đã hoàn tất chưa
            var outboundOrders = await _outboundOrderRepository
                .FindByCondition(x => x.SalesOrderId == order.SalesOrderId && !x.IsDeleted, false, x => x.OutboundOrderStatus, x => x.OutboundOrderItems)
                .ToListAsync();

            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(order.SalesOrderId);
            if (salesOrder != null && !salesOrder.IsDeleted)
            {
                bool allCompleted = true;
                foreach (var salesItem in salesOrder.SalesOrderItems.Where(i => !i.IsDeleted))
                {
                    var totalDispatched = outboundOrders
                        .Where(o => o.OutboundOrderStatus?.Code == OutboundOrderStatusNames.Completed)
                        .SelectMany(o => o.OutboundOrderItems)
                        .Where(item => item.ProductVariantId == salesItem.ProductVariantId)
                        .Sum(item => item.QuantityPicked);

                    if (totalDispatched < salesItem.QuantityOrdered)
                    {
                        allCompleted = false;
                        break;
                    }
                }

                if (allCompleted)
                {
                    salesOrder.StatusId = await GetSalesStatusIdAsync(SalesOrderStatusNames.Completed);
                    salesOrder.LastModifiedDate = now;
                    salesOrder.UpdatedBy = userId;
                    await _salesOrderRepository.UpdateAsync(salesOrder);
                }
            }

            await _outboundOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }

        if (_scheduledJobService != null && paidPartyDebt != null)
        {
            _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s =>
                s.EvaluatePartyDebtAsync(paidPartyDebt.Id, CancellationToken.None));
        }

        return ApiResponse.Success(new
        {
            PaymentAmount = dto.PaymentAmount,
            RemainingDebt = remainingDebt
        }, dto.PaymentAmount > 0
            ? "Xác nhận giao hàng và ghi nhận thanh toán thành công."
            : "Xác nhận giao hàng thành công.");
    }

    public async Task<ApiResponse> FailDeliveryAsync(int id, FailDeliveryDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Dispatched)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái DISPATCHED để xác nhận giao hàng thất bại. Trạng thái hiện tại: '{order.OutboundOrderStatus?.Name}'.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        await using var tx = await _outboundOrderRepository.BeginTransactionAsync();
        try
        {
            // Cập nhật trạng thái phiếu xuất sang DELIVERY_FAILED
            order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.DeliveryFailed);
            order.DeliveryNote = $"Giao hàng thất bại: {dto.Reason}";
            order.LastModifiedDate = now;
            order.UpdatedBy = userId;
            await _outboundOrderRepository.UpdateAsync(order);

            // Hoàn trả lại tồn kho vật lý (hoàn nhập số lượng thực tế đã trừ khi dispatch)
            // Nạp 1 lượt Inventory & PaddyLot liên quan (thay GetById trong vòng lặp -> tránh N+1).
            var failActiveItems = order.OutboundOrderItems.Where(i => !i.IsDeleted).ToList();
            var failAllocs = failActiveItems.SelectMany(ActiveAllocations).ToList();
            var failInvIds = failAllocs.Select(a => a.InventoryId).Distinct().ToList();
            var failInvMap = (await _inventoryRepository
                    .FindByCondition(i => failInvIds.Contains(i.Id))
                    .ToListAsync())
                .ToDictionary(i => i.Id);
            var failLotIds = failAllocs.Where(a => a.PaddyLotId.HasValue)
                .Select(a => a.PaddyLotId!.Value).Distinct().ToList();
            var failLotMap = failLotIds.Count > 0
                ? (await _paddyLotRepository
                    .FindByCondition(l => failLotIds.Contains(l.Id))
                    .ToListAsync())
                    .ToDictionary(l => l.Id)
                : new Dictionary<int, PaddyLot>();

            foreach (var item in failActiveItems)
            {
                foreach (var alloc in ActiveAllocations(item))
                {
                    if (failInvMap.TryGetValue(alloc.InventoryId, out var inv) && inv != null && !inv.IsDeleted)
                    {
                        if (!inv.LocationId.HasValue || _locationRepository == null)
                            throw new InvalidOperationException($"Tồn hoàn của allocation #{alloc.Id} chưa có vị trí hợp lệ.");
                        var returnLocation = await _locationRepository.GetByIdAsync(inv.LocationId.Value)
                            ?? throw new InvalidOperationException($"Không tìm thấy vị trí hoàn của allocation #{alloc.Id}.");
                        if (!returnLocation.IsActive || returnLocation.IsDeleted || returnLocation.IsQuarantine ||
                            returnLocation.IsOutboundStaging || returnLocation.OutboundLockOrderId.HasValue ||
                            (returnLocation.CurrentProductVariantId.HasValue &&
                             returnLocation.CurrentProductVariantId != inv.ProductVariantId) ||
                            (returnLocation.MaxCapacity.HasValue &&
                             returnLocation.CurrentOccupancy + alloc.QuantityPicked > returnLocation.MaxCapacity.Value + 0.0005m))
                            throw new InvalidOperationException(
                                $"Vị trí '{FormatLocationCode(returnLocation)}' hiện không thể nhận hàng giao thất bại.");

                        var capacityUpdated = await _locationRepository.UpdateCapacitySafetyAsync(
                            returnLocation.Id, inv.WarehouseId, alloc.QuantityPicked,
                            inv.ProductVariantId, false, userId);
                        if (capacityUpdated != 1)
                            throw new InvalidOperationException(
                                $"Vị trí '{FormatLocationCode(returnLocation)}' vừa thay đổi và không còn đủ điều kiện nhận hàng giao thất bại.");

                        var before = inv.QuantityOnHand;
                        inv.QuantityOnHand += alloc.QuantityPicked;
                        inv.LastModifiedDate = now;
                        inv.UpdatedBy = userId;
                        await _inventoryRepository.UpdateAsync(inv);
                        if (alloc.PaddyLotId.HasValue && inv.LocationId.HasValue)
                            await RestockPhysicalBagsAsync(alloc.PaddyLotId.Value, inv.LocationId.Value, alloc.QuantityPicked,
                                order.Id, alloc.Id, userId, now);

                        // Ghi nhận lịch sử hoàn trả tồn kho (CUSTOMER_RETURN_RESTOCK)
                        var txn = new InventoryTransaction
                        {
                            InventoryId = inv.Id,
                            WarehouseId = inv.WarehouseId,
                            LocationId = inv.LocationId,
                            ProductVariantId = inv.ProductVariantId,
                            TransactionType = InventoryTransactionTypeConstants.CustomerReturnRestock,
                            ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                            ReferenceId = order.Id,
                            ReferenceItemId = alloc.Id,
                            PaddyLotId = alloc.PaddyLotId,
                            Quantity = alloc.QuantityPicked,
                            BeforeQuantity = before,
                            AfterQuantity = inv.QuantityOnHand,
                            Note = $"Hoàn trả tồn do giao hàng thất bại: {dto.Reason}",
                            CreatedDate = now,
                            CreatedBy = userId
                        };
                        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txn);
                    }

                    // Hoàn trả tồn lô lúa/gạo nếu có
                    if (alloc.PaddyLotId.HasValue)
                    {
                        if (failLotMap.TryGetValue(alloc.PaddyLotId.Value, out var lot) && lot != null && !lot.IsDeleted)
                        {
                            lot.RemainingWeightKg += alloc.QuantityPicked;
                            await _paddyLotRepository.UpdateAsync(lot);
                        }
                    }
                }
            }

            // Hoàn trả lại công nợ (nếu có)
            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(order.SalesOrderId);
            PartyDebt? partyDebt = null;
            if (salesOrder != null && !salesOrder.IsDeleted)
            {
                var amountToCharge = order.TotalDispatchedSaleValue;
                if (amountToCharge > 0)
                {
                    // Kiểm tra xem đã có giao dịch CHARGE cho phiếu xuất này chưa
                    var existingCharge = await _debtTransactionRepository.FirstOrDefaultAsync(x =>
                        x.RefType == "OUTBOUND_ORDER" && x.RefId == order.Id && x.TransactionType == LookupCodes.DebtTransactionType.Charge && !x.IsDeleted);

                    if (existingCharge != null)
                    {
                        partyDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
                            !x.IsDeleted &&
                            x.PartyType == "CUSTOMER" &&
                            x.PartyId == salesOrder.CustomerId &&
                            x.Direction == "RECEIVABLE" &&
                            x.IsActive);

                        if (partyDebt != null)
                        {
                            partyDebt.CurrentBalance -= amountToCharge;
                            partyDebt.LastModifiedDate = now;
                            partyDebt.UpdatedBy = userId;
                            await _partyDebtRepository.UpdateAsync(partyDebt);

                            var reversalTx = new DebtTransaction
                            {
                                PartyDebtId = partyDebt.Id,
                                TransactionType = "RETURN_CREDIT",
                                Amount = amountToCharge,
                                BalanceAfter = partyDebt.CurrentBalance,
                                RefType = "OUTBOUND_ORDER",
                                RefId = order.Id,
                                TransactionDate = now,
                                Note = $"Hoàn trả công nợ do giao hàng thất bại (Phiếu xuất {order.Id})",
                                CreatedDate = now,
                                CreatedBy = userId
                            };
                            await _debtTransactionRepository.CreateAsync(reversalTx);
                        }
                    }
                }

                // Chuyển trạng thái SalesOrder trở lại PREPARING nếu nó đang ở DELIVERING
                if (salesOrder.Status?.Code == SalesOrderStatusNames.Delivering)
                {
                    // Kiểm tra xem còn phiếu xuất nào khác đang giao (DISPATCHED) không
                    var otherDelivering = await _outboundOrderRepository.AnyAsync(x =>
                        x.SalesOrderId == salesOrder.Id &&
                        x.Id != order.Id &&
                        !x.IsDeleted &&
                        x.OutboundOrderStatus != null &&
                        x.OutboundOrderStatus.Code == OutboundOrderStatusNames.Dispatched);

                    if (!otherDelivering)
                    {
                        salesOrder.StatusId = await GetSalesStatusIdAsync(SalesOrderStatusNames.Preparing);
                        salesOrder.LastModifiedDate = now;
                        salesOrder.UpdatedBy = userId;
                        await _salesOrderRepository.UpdateAsync(salesOrder);
                    }
                }
            }

            await _outboundOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            // Enqueue targeted background job evaluation for JOB-04 after transaction completes
            if (_scheduledJobService != null && partyDebt != null)
            {
                _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(partyDebt.Id, CancellationToken.None));
            }

            return ApiResponse.Success(message: "Xác nhận giao hàng thất bại thành công.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task RestockPhysicalBagsAsync(int lotId, int locationId, decimal quantityKg,
        int outboundOrderId, int allocationId, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagContentRepository == null || quantityKg <= 0) return;
        var lot = await _paddyLotRepository.GetByIdAsync(lotId);
        if (lot == null) throw new InvalidOperationException($"Không tìm thấy lô {lotId} để hoàn bao.");
        var sample = await _bagRepository.FirstOrDefaultAsync(x => x.LotId == lotId && !x.IsDeleted, false);
        var standard = sample?.StandardWeightKg;
        var remaining = quantityKg;
        if (standard.HasValue)
        {
            var openBags = await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.BagKind == PaddyLotBagKinds.Finished && !x.IsFull && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, true)
                .Include(x => x.Lot).Where(x => x.Lot.ProductVariantId == lot.ProductVariantId).OrderByDescending(x => x.StackOrder).ToListAsync();
            if (openBags.Count > 1)
                throw new InvalidOperationException($"Vị trí {locationId} có nhiều hơn một bao mở của SKU {lot.ProductVariantId}.");
            var open = openBags.FirstOrDefault();
            if (open != null)
            {
                var topOrder = await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                    .MaxAsync(x => (int?)x.StackOrder) ?? 0;
                if (open.StackOrder != topOrder)
                    throw new InvalidOperationException($"Bao mở #{open.BagNo} đang bị chặn nên không thể hoàn hàng vào bao.");
                var topUp = Math.Min(remaining, standard.Value - open.WeightKg);
                if (topUp > 0)
                {
                    var beforeWeight = open.WeightKg;
                    await _bagContentRepository.CreateAsync(new PaddyLotBagContent { BagId = open.Id, LotId = lotId, WeightKg = topUp, CreatedBy = userId, CreatedDate = now });
                    open.WeightKg += topUp; open.IsFull = open.WeightKg >= standard; open.OpenBagKey = open.IsFull ? null : $"{lot.ProductVariantId}:{lot.WarehouseId}";
                    await _bagRepository.UpdateAsync(open); remaining -= topUp;
                    if (_bagMovementRepository != null)
                        await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                        {
                            BagId = open.Id, MovementType = PaddyLotBagMovementTypes.DeliveryRestock,
                            FromLocationId = null, ToLocationId = locationId,
                            WeightKg = topUp, BeforeWeightKg = beforeWeight, AfterWeightKg = open.WeightKg,
                            ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                            ReferenceId = outboundOrderId, ReferenceItemId = allocationId,
                            Note = $"Hoàn {topUp:0.###} kg thành phần lô {lot.LotCode} sau giao thất bại",
                            CreatedBy = userId, CreatedDate = now
                        });
                }
            }
        }
        var bagNo = (await _bagRepository.FindByCondition(x => x.LotId == lotId && !x.IsDeleted).MaxAsync(x => (int?)x.BagNo) ?? 0) + 1;
        var stack = (await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted).MaxAsync(x => (int?)x.StackOrder) ?? 0) + 1;
        while (remaining > 0.0005m)
        {
            var weight = standard.HasValue ? Math.Min(remaining, standard.Value) : remaining;
            var bag = new PaddyLotBag { LotId = lotId, BagNo = bagNo++, WeightKg = weight, LocationId = locationId, Status = PaddyLotBagStatuses.Stored, StackOrder = stack++, StandardWeightKg = standard, IsFull = !standard.HasValue || weight >= standard, BagKind = standard.HasValue ? PaddyLotBagKinds.Finished : PaddyLotBagKinds.Purchase, OpenBagKey = standard.HasValue && weight < standard ? $"{lot.ProductVariantId}:{lot.WarehouseId}" : null, QrCode = $"PLB-{Guid.NewGuid():N}".ToUpperInvariant(), CreatedBy = userId, CreatedDate = now };
            await _bagRepository.CreateAsync(bag); await _bagRepository.SaveChangesAsync();
            await _bagContentRepository.CreateAsync(new PaddyLotBagContent { BagId = bag.Id, LotId = lotId, WeightKg = weight, CreatedBy = userId, CreatedDate = now });
            if (_bagMovementRepository != null)
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = bag.Id, MovementType = PaddyLotBagMovementTypes.DeliveryRestock,
                    FromLocationId = null, ToLocationId = locationId,
                    WeightKg = weight, BeforeWeightKg = 0, AfterWeightKg = weight,
                    ReferenceType = InventoryReferenceTypeConstants.OutboundOrder,
                    ReferenceId = outboundOrderId, ReferenceItemId = allocationId,
                    Note = $"Tái đóng bao hoàn từ thành phần lô {lot.LotCode}",
                    CreatedBy = userId, CreatedDate = now
                });
            remaining -= weight;
        }
        await _bagContentRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Hủy OutboundOrder. Nếu đang PICKING/PACKED → giải phóng QuantityReserved chưa dispatch.
    /// </summary>
    public async Task<ApiResponse> CancelAsync(int id, string? reason = null)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        var trimmedReason = reason?.Trim();

        var cancellableStates = new[]
        {
            OutboundOrderStatusNames.Draft,
            OutboundOrderStatusNames.Picking,
            OutboundOrderStatusNames.Packed
        };
        if (!cancellableStates.Contains(order.OutboundOrderStatus?.Code))
            return ApiResponse.Conflict(
                $"Không thể hủy phiếu xuất ở trạng thái '{order.OutboundOrderStatus?.Name}'.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        await using var tx = await _outboundOrderRepository.BeginTransactionAsync();
        try
        {
            var returnedFromStaging = order.OutboundOrderStatus?.Code == OutboundOrderStatusNames.Packed &&
                                      await ReturnStagedBagsAsync(order, userId, now);

            // PICKING còn giữ tồn tại cột nguồn. PACKED mới chỉ trừ trực tiếp nếu là dữ liệu cũ chưa staging.
            if (order.OutboundOrderStatus?.Code is OutboundOrderStatusNames.Picking or OutboundOrderStatusNames.Packed)
            {
                foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
                {
                    foreach (var alloc in ActiveAllocations(item))
                    {
                        if (returnedFromStaging) continue;
                        var inv = alloc.Inventory;
                        if (inv == null) continue;
                        inv.QuantityReserved = Math.Max(0, inv.QuantityReserved - alloc.QuantityAllocated);
                        inv.LastModifiedDate = now;
                        inv.UpdatedBy = userId;
                        await _inventoryRepository.UpdateAsync(inv);
                    }
                }
            }

            await ReleaseOutboundColumnLocksAsync(order, now, userId);
            order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Cancelled);
            if (!string.IsNullOrWhiteSpace(trimmedReason))
                order.CancelReason = trimmedReason.Length > 500 ? trimmedReason[..500] : trimmedReason;
            order.LastModifiedDate = now;
            order.UpdatedBy = userId;
            await _outboundOrderRepository.UpdateAsync(order);
            await _outboundOrderRepository.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            return ApiResponse.Success(message: "Phiếu xuất đã được hủy.");
        }
        catch (InvalidOperationException ex)
        {
            if (tx != null) await tx.RollbackAsync();
            return ApiResponse.UnprocessableEntity(ex.Message, ApiCodeConstants.OutboundOrder.InvalidRequest);
        }
        catch (Exception)
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
    }
}

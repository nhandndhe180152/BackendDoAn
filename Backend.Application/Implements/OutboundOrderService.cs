using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        INotificationDispatcher notificationDispatcher)
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
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;

    private async Task<int> GetOutboundStatusIdAsync(string name)
    {
        var s = await _outboundStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"OutboundOrderStatus '{name}' not found.");
    }

    private async Task<int> GetSalesStatusIdAsync(string name)
    {
        var s = await _salesOrderStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"SalesOrderStatus '{name}' not found.");
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
            OutboundStatusColor  = o.OutboundOrderStatus?.Color ?? "",
            WarehouseId          = o.WarehouseId,
            WarehouseName        = o.Warehouse?.Name ?? "",
            TotalDispatchedValue = o.TotalDispatchedValue,
            CompletedDate        = o.CompletedDate,
            Note                 = o.Note,
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
                Allocations = i.Allocations.Select(a => new OutboundOrderItemAllocationDto
                {
                    Id                = a.Id,
                    InventoryId       = a.InventoryId,
                    PaddyLotId        = a.PaddyLotId,
                    PaddyLotCode      = a.PaddyLot?.LotCode,
                    LocationId        = a.LocationId,
                    LocationCode      = a.Location?.SlotCode,
                    QuantityAllocated = a.QuantityAllocated,
                    QuantityPicked    = a.QuantityPicked,
                    UnitCostPrice     = a.UnitCostPrice
                }).ToList()
            }).ToList()
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetPagedAsync(OutboundOrderPagedQuery query)
    {
        var skip  = (query.Page - 1) * query.PageSize;
        var total = await _outboundOrderRepository.CountAsync(query.Keyword);
        var list  = await _outboundOrderRepository.GetPagedListAsync(query.Keyword, skip, query.PageSize);

        var dtos = list.Select(o => new OutboundOrderListDto
        {
            Id                   = o.Id,
            SalesOrderId         = o.SalesOrderId,
            SOCode               = o.SalesOrder?.SOCode ?? "",
            CustomerName         = o.SalesOrder?.Customer?.Name ?? "",
            OutboundStatusId     = o.OutboundOrderStatusId,
            OutboundStatusName   = o.OutboundOrderStatus?.Name ?? "",
            OutboundStatusColor  = o.OutboundOrderStatus?.Color ?? "",
            WarehouseId          = o.WarehouseId,
            WarehouseName        = o.Warehouse?.Name,
            TotalDispatchedValue = o.TotalDispatchedValue,
            CompletedDate        = o.CompletedDate,
            Note                 = o.Note,
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

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phân bổ lot/vị trí cho từng OutboundOrderItem → trạng thái PICKING.
    /// </summary>
    public async Task<ApiResponse> AllocateAsync(int id, AllocateOutboundDto dto)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Name != OutboundOrderStatusNames.Draft)
            return ApiResponse.Conflict(
                $"Phiếu xuất đang ở trạng thái '{order.OutboundOrderStatus?.Name}', chỉ có thể phân bổ khi ở DRAFT.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        foreach (var allocItem in dto.Allocations)
        {
            var item = order.OutboundOrderItems.FirstOrDefault(i => !i.IsDeleted && i.Id == allocItem.OutboundOrderItemId);
            if (item == null)
                return ApiResponse.BadRequest(
                    $"OutboundOrderItem ID {allocItem.OutboundOrderItemId} không tồn tại trong phiếu.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            // Validate tổng quantity allocated không vượt quantity ordered cho item này
            var alreadyAllocated = item.Allocations.Sum(a => a.QuantityAllocated);
            var newAllocTotal    = allocItem.Lots.Sum(l => l.QuantityAllocated);
            if (alreadyAllocated + newAllocTotal > item.QuantityOrdered)
                return ApiResponse.UnprocessableEntity(
                    $"Tổng số lượng phân bổ ({alreadyAllocated + newAllocTotal}) vượt quá số lượng đặt ({item.QuantityOrdered}) " +
                    $"cho OutboundOrderItem ID {item.Id}.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            // Tính weighted-average cost trước khi tạo allocation
            decimal totalAllocQty   = alreadyAllocated;
            decimal weightedCostSum = alreadyAllocated * item.UnitCostPrice;

            foreach (var lot in allocItem.Lots)
            {
                var inv = await _inventoryRepository.GetByIdAsync(lot.InventoryId,
                    x => x.Location,
                    x => x.PaddyLot,
                    x => x.PaddyLot.Status);
                if (inv == null || inv.IsDeleted)
                    return ApiResponse.BadRequest(
                        $"Inventory ID {lot.InventoryId} không tồn tại.",
                        ApiCodeConstants.OutboundOrder.InvalidRequest);

                if (inv.LocationId == null)
                    return ApiResponse.BadRequest(
                        $"Inventory ID {lot.InventoryId} chưa có vị trí.",
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

        order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Picking);
        order.LastModifiedDate      = now;
        order.UpdatedBy             = userId;
        await _outboundOrderRepository.UpdateAsync(order);

        try
        {
            await _outboundOrderRepository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
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

        if (order.OutboundOrderStatus?.Name != OutboundOrderStatusNames.Picking)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái PICKING để cập nhật picking.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        foreach (var pickDto in dto.Picks)
        {
            var allocation = order.OutboundOrderItems
                .SelectMany(i => i.Allocations)
                .FirstOrDefault(a => a.Id == pickDto.AllocationId);

            if (allocation == null)
                return ApiResponse.BadRequest(
                    $"Allocation ID {pickDto.AllocationId} không tồn tại.",
                    ApiCodeConstants.OutboundOrder.InvalidRequest);

            allocation.QuantityPicked  = pickDto.QuantityPicked;
            allocation.LastModifiedDate = now;
            allocation.UpdatedBy       = userId;
            await _allocationRepository.UpdateAsync(allocation);
        }

        // Cập nhật QuantityPicked tổng cho mỗi OutboundOrderItem
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            item.QuantityPicked  = item.Allocations.Sum(a => a.QuantityPicked);
            item.LastModifiedDate = now;
            item.UpdatedBy       = userId;
        }

        order.LastModifiedDate = now;
        order.UpdatedBy        = userId;
        await _outboundOrderRepository.UpdateAsync(order);
        await _outboundOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Cập nhật số lượng picking thành công.");
    }

    /// <summary>
    /// Xác nhận đóng gói xong → PACKED. Validate tất cả items đã pick đủ.
    /// </summary>
    public async Task<ApiResponse> ConfirmPackingAsync(int id)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        if (order.OutboundOrderStatus?.Name != OutboundOrderStatusNames.Picking)
            return ApiResponse.Conflict(
                "Phiếu xuất phải ở trạng thái PICKING để xác nhận đóng gói.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        // Validate mỗi item đã pick ít nhất bằng qty ordered
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            var picked = item.Allocations.Sum(a => a.QuantityPicked);
            if (picked < item.QuantityOrdered)
                return ApiResponse.UnprocessableEntity(
                    $"Sản phẩm '{item.ProductVariant?.Name}' chưa pick đủ. " +
                    $"Cần: {item.QuantityOrdered}, Đã lấy: {picked}.",
                    ApiCodeConstants.OutboundOrder.PickedQuantityMismatch);
        }

        var now = DateTimeHelper.VietnamNow();
        order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Packed);
        order.LastModifiedDate      = now;
        order.UpdatedBy             = GetCurrentUserId();
        await _outboundOrderRepository.UpdateAsync(order);
        await _outboundOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Đóng gói hoàn tất. Trạng thái: PACKED.");
    }

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
        if (order.OutboundOrderStatus?.Name != OutboundOrderStatusNames.Packed)
            return ApiResponse.Conflict(
                $"Phiếu xuất phải ở trạng thái PACKED để xác nhận xuất kho. " +
                $"Trạng thái hiện tại: {order.OutboundOrderStatus?.Name}.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        // 2. Validate lot còn sellable
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            foreach (var alloc in item.Allocations)
            {
                var lotIsQuarantined = (alloc.PaddyLot != null && alloc.PaddyLot.Status?.Code == LotStatusCodeConstants.Quarantine)
                    || (alloc.Location != null && alloc.Location.IsQuarantine);

                if (lotIsQuarantined || (alloc.PaddyLot != null && (alloc.PaddyLot.Status == null || !alloc.PaddyLot.Status.IsSellable)))
                    return ApiResponse.UnprocessableEntity(
                        $"Lô {alloc.PaddyLot?.LotCode ?? "không xác định"} nằm ở vị trí cách ly hoặc có trạng thái không hợp lệ để xuất bán.",
                        ApiCodeConstants.OutboundOrder.LotQuarantined);
            }
        }

        // 3. Validate qty picked khớp với qty ordered
        foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
        {
            var picked = item.Allocations.Sum(a => a.QuantityPicked);
            if (Math.Abs(picked - item.QuantityOrdered) > 0.001m)
                return ApiResponse.UnprocessableEntity(
                    $"Số lượng thực lấy ({picked}) không khớp với số lượng đặt ({item.QuantityOrdered}) " +
                    $"cho sản phẩm '{item.ProductVariant?.Name}'.",
                    ApiCodeConstants.OutboundOrder.PickedQuantityMismatch);
        }

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        decimal totalDispatchedValue = 0;

        // Bọc toàn bộ trong 1 DB transaction thật — đảm bảo nguyên tử
        await using var tx = await _outboundOrderRepository.BeginTransactionAsync();
        try
        {
            // 4. Với từng allocation: giảm tồn + tạo giao dịch
            foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
            {
                foreach (var alloc in item.Allocations)
                {
                    var inv = alloc.Inventory;
                    if (inv == null || inv.IsDeleted)
                    {
                        await _outboundOrderRepository.RollbackTransactionAsync();
                        return ApiResponse.BadRequest(
                            $"Inventory ID {alloc.InventoryId} không còn tồn tại.",
                            ApiCodeConstants.OutboundOrder.InvalidRequest);
                    }

                    var before = inv.QuantityOnHand;

                    // 5. GIẢM TỒN KHO VẬT LÝ (Inventory):
                    // - Trừ số lượng thực xuất (QuantityPicked) khỏi tồn kho vật lý của kệ.
                    // - Trừ số lượng giữ trước (QuantityAllocated) khỏi hàng đang giữ trước.
                    inv.QuantityOnHand    -= alloc.QuantityPicked;
                    inv.QuantityReserved  -= alloc.QuantityAllocated;

                    // Đảm bảo tồn kho vật lý và số lượng giữ trước không bị âm
                    if (inv.QuantityOnHand    < 0) inv.QuantityOnHand    = 0;
                    if (inv.QuantityReserved  < 0) inv.QuantityReserved  = 0;

                    inv.LastModifiedDate = now;
                    inv.UpdatedBy        = userId;
                    await _inventoryRepository.UpdateAsync(inv);

                    // ĐỒNG BỘ HÓA TỒN LÔ HÀNG (PaddyLot):
                    // - Vì lô hàng thực tế đã xuất ra khỏi kho, khối lượng còn lại của lô (RemainingWeightKg)
                    //   phải được khấu trừ tương ứng với số lượng xuất kho vật lý.
                    // - Điều này đảm bảo tính đồng nhất giữa Dashboard (đọc từ PaddyLot) và Giám sát kho (đọc từ Inventory).
                    if (alloc.PaddyLotId.HasValue)
                    {
                        var lot = await _paddyLotRepository.GetByIdAsync(alloc.PaddyLotId.Value);
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
                        ReferenceItemId  = item.Id,
                        PaddyLotId       = alloc.PaddyLotId,
                        Quantity         = -alloc.QuantityPicked,   // âm = xuất
                        BeforeQuantity   = before,
                        AfterQuantity    = inv.QuantityOnHand,
                        Note             = dto.Note,
                        CreatedDate      = now,
                        CreatedBy        = userId
                    };
                    await _inventoryTransactionRepository.CreateAsync(txn);

                    totalDispatchedValue += alloc.QuantityPicked * alloc.UnitCostPrice;
                }

                // 7. Cập nhật OutboundOrderItem.QuantityPicked
                item.QuantityPicked    = item.Allocations.Sum(a => a.QuantityPicked);
                item.LastModifiedDate  = now;
                item.UpdatedBy         = userId;
            }

            // 8. Cập nhật OutboundOrder → DISPATCHED
            order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Dispatched);
            order.TotalDispatchedValue  = totalDispatchedValue;
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

                // 10. Tạo công nợ phải thu nếu khách chưa trả đủ
                var remaining = salesOrder.TotalAmount - (salesOrder.DepositAmount ?? 0);
                if (remaining > 0)
                {
                    // Tìm hoặc tạo PartyDebt RECEIVABLE
                    var partyDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
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
                        // Lưu để lấy Id của PartyDebt mới (vẫn trong transaction)
                        await _outboundOrderRepository.SaveChangesAsync();
                    }

                    partyDebt.CurrentBalance  += remaining;
                    partyDebt.LastModifiedDate  = now;
                    partyDebt.UpdatedBy         = userId;
                    await _partyDebtRepository.UpdateAsync(partyDebt);

                    // Tạo DebtTransaction CHARGE
                    await _debtTransactionRepository.CreateAsync(new DebtTransaction
                    {
                        PartyDebtId     = partyDebt.Id,
                        TransactionType = "CHARGE",
                        Amount          = remaining,
                        BalanceAfter    = partyDebt.CurrentBalance,
                        RefType         = "SALES_ORDER",
                        RefId           = salesOrder.Id,
                        TransactionDate = now,
                        Note            = $"Công nợ phát sinh từ đơn bán {salesOrder.SOCode}",
                        CreatedDate     = now,
                        CreatedBy       = userId
                    });
                }
            }

            // Ghi toàn bộ thay đổi vào DB rồi commit trong 1 transaction
            await _outboundOrderRepository.SaveChangesAsync();
            await _outboundOrderRepository.EndTransactionAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await _outboundOrderRepository.RollbackTransactionAsync();
            return ApiResponse.Conflict(
                "Tồn kho đã thay đổi trong lúc xử lý. Vui lòng tải lại và thử lại.",
                ApiCodeConstants.OutboundOrder.ConcurrencyConflict);
        }
        catch (Exception)
        {
            await _outboundOrderRepository.RollbackTransactionAsync();
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
            $"/admin/outbound-orders/{order.Id}",
            userId);

        return ApiResponse.Success(
            new { TotalDispatchedValue = totalDispatchedValue },
            "Xác nhận xuất kho thành công.");
    }

    /// <summary>
    /// Hủy OutboundOrder. Nếu đang PICKING/PACKED → giải phóng QuantityReserved chưa dispatch.
    /// </summary>
    public async Task<ApiResponse> CancelAsync(int id)
    {
        var order = await _outboundOrderRepository.GetByIdDetailAsync(id);
        if (order == null || order.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy phiếu xuất.", ApiCodeConstants.OutboundOrder.NotFound);

        var cancellableStates = new[]
        {
            OutboundOrderStatusNames.Draft,
            OutboundOrderStatusNames.Picking,
            OutboundOrderStatusNames.Packed
        };
        if (!cancellableStates.Contains(order.OutboundOrderStatus?.Name))
            return ApiResponse.Conflict(
                $"Không thể hủy phiếu xuất ở trạng thái '{order.OutboundOrderStatus?.Name}'.",
                ApiCodeConstants.OutboundOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        // Nếu đã phân bổ → giải phóng reserved
        if (order.OutboundOrderStatus?.Name is OutboundOrderStatusNames.Picking or OutboundOrderStatusNames.Packed)
        {
            foreach (var item in order.OutboundOrderItems.Where(i => !i.IsDeleted))
            {
                foreach (var alloc in item.Allocations)
                {
                    var inv = alloc.Inventory;
                    if (inv == null) continue;

                    inv.QuantityReserved  -= alloc.QuantityAllocated;
                    if (inv.QuantityReserved < 0) inv.QuantityReserved = 0;
                    inv.LastModifiedDate   = now;
                    inv.UpdatedBy          = userId;
                    await _inventoryRepository.UpdateAsync(inv);
                }
            }
        }

        order.OutboundOrderStatusId = await GetOutboundStatusIdAsync(OutboundOrderStatusNames.Cancelled);
        order.LastModifiedDate      = now;
        order.UpdatedBy             = userId;
        await _outboundOrderRepository.UpdateAsync(order);
        await _outboundOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Phiếu xuất đã được hủy.");
    }
}

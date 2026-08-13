using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.SalesOrders;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ đơn bán gạo (SalesOrder).
/// Flow: NEW → PENDING_CONFIRM → RESERVED → PREPARING → DELIVERING → COMPLETED
/// CANCELLED là nhánh kết thúc thay thế.
/// </summary>
public class SalesOrderService : ISalesOrderService
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IRepositoryBase<SalesOrderItem, int> _salesOrderItemRepository;
    private readonly IRepositoryBase<SalesOrderStatus, int> _salesOrderStatusRepository;
    private readonly IRepositoryBase<OutboundOrder, int> _outboundOrderRepository;
    private readonly IRepositoryBase<OutboundOrderItem, int> _outboundOrderItemRepository;
    private readonly IRepositoryBase<OutboundOrderStatus, int> _outboundOrderStatusRepository;
    private readonly IRepositoryBase<Customer, int> _customerRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IPartyDebtRepository _partyDebtRepository;
    private readonly IRepositoryBase<MillingOrder, int> _millingOrderRepository;
    private readonly IRepositoryBase<Organization, int> _organizationRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationDispatcher _notificationDispatcher;

    public SalesOrderService(
        ISalesOrderRepository salesOrderRepository,
        IRepositoryBase<SalesOrderItem, int> salesOrderItemRepository,
        IRepositoryBase<SalesOrderStatus, int> salesOrderStatusRepository,
        IRepositoryBase<OutboundOrder, int> outboundOrderRepository,
        IRepositoryBase<OutboundOrderItem, int> outboundOrderItemRepository,
        IRepositoryBase<OutboundOrderStatus, int> outboundOrderStatusRepository,
        IRepositoryBase<Customer, int> customerRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IPartyDebtRepository partyDebtRepository,
        IRepositoryBase<MillingOrder, int> millingOrderRepository,
        IRepositoryBase<Organization, int> organizationRepository,
        IHttpContextAccessor httpContextAccessor,
        INotificationDispatcher notificationDispatcher)
    {
        _salesOrderRepository        = salesOrderRepository;
        _salesOrderItemRepository    = salesOrderItemRepository;
        _salesOrderStatusRepository  = salesOrderStatusRepository;
        _outboundOrderRepository     = outboundOrderRepository;
        _outboundOrderItemRepository = outboundOrderItemRepository;
        _outboundOrderStatusRepository = outboundOrderStatusRepository;
        _customerRepository          = customerRepository;
        _inventoryRepository         = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _partyDebtRepository         = partyDebtRepository;
        _millingOrderRepository      = millingOrderRepository;
        _organizationRepository      = organizationRepository;
        _httpContextAccessor         = httpContextAccessor;
        _notificationDispatcher      = notificationDispatcher;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;

    private async Task<int> GetCurrentOrganizationIdAsync()
    {
        var officeIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimNames.OFFICE_ID)?.Value;
        if (int.TryParse(officeIdStr, out var id) && id > 0)
        {
            return id;
        }

        var defaultOrg = await _organizationRepository.FirstOrDefaultAsync(
            x => x.IsActive && !x.IsDeleted);

        return defaultOrg?.Id 
            ?? throw new InvalidOperationException("Không tìm thấy tổ chức hoạt động nào trong hệ thống.");
    }

    private async Task<int> GetStatusIdAsync(string code)
    {
        var status = await _salesOrderStatusRepository.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
        return status?.Id ?? throw new InvalidOperationException($"SalesOrderStatus with code '{code}' not found.");
    }

    private static SalesOrderDetailDto MapDetail(SalesOrder so)
    {
        var remaining = so.TotalAmount - (so.DepositAmount ?? 0);
        return new SalesOrderDetailDto
        {
            Id                   = so.Id,
            SOCode               = so.SOCode,
            CustomerId           = so.CustomerId,
            CustomerName         = so.Customer?.Name ?? "",
            CustomerPhone        = so.Customer?.Phone,
            StatusId             = so.StatusId,
            StatusName           = so.Status?.Name ?? "",
            StatusCode           = so.Status?.Code ?? "",
            StatusColor          = so.Status?.Color ?? "",
            Channel              = so.Channel,
            WarehouseId          = so.WarehouseId,
            WarehouseName        = so.Warehouse?.Name,
            OrderDate            = so.OrderDate,
            ExpectedDeliveryDate = so.ExpectedDeliveryDate,
            RequiresMilling      = so.RequiresMilling,
            TotalAmount          = so.TotalAmount,
            DepositAmount        = so.DepositAmount,
            RemainingAmount      = remaining > 0 ? remaining : 0,
            ShippingAddress      = so.ShippingAddress,
            Note                 = so.Note,
            CancelReason         = so.CancelReason,
            CreatedDate          = so.CreatedDate,
            Items = so.SalesOrderItems.Select(i => new SalesOrderItemDto
            {
                Id                 = i.Id,
                ProductVariantId   = i.ProductVariantId,
                ProductVariantName = i.ProductVariant?.Name ?? "",
                SKU                = i.ProductVariant?.SKU,
                QuantityOrdered    = i.QuantityOrdered,
                UnitSalePrice      = i.UnitSalePrice,
                DiscountAmount     = i.DiscountAmount ?? 0,
                LineAmount         = i.LineAmount,
                Note               = i.Note
            }).ToList(),
            OutboundOrders = so.OutboundOrders.Select(o => new SalesOrderOutboundSummaryDto
            {
                Id                   = o.Id,
                OutboundStatusId     = o.OutboundOrderStatusId,
                OutboundStatusName   = o.OutboundOrderStatus?.Name ?? "",
                OutboundStatusCode   = o.OutboundOrderStatus?.Code ?? "",
                TotalDispatchedValue = o.TotalDispatchedValue,
                TotalDispatchedSaleValue = o.TotalDispatchedSaleValue,
                CompletedDate        = o.CompletedDate
            }).ToList()
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetPagedAsync(SalesOrderPagedQuery query)
    {
        // Chuẩn hóa tham số trang để client gửi page=0 hay pageSize âm không làm
        // vỡ Skip/Take.
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;
        var skip = (page - 1) * pageSize;

        var total = await _salesOrderRepository.CountAsync(
            query.Keyword, query.StatusId, query.Channel);
        var list = await _salesOrderRepository.GetPagedListAsync(
            query.Keyword, skip, pageSize, query.StatusId, query.Channel);

        var dtos = list.Select(so =>
        {
            var riceItems = so.SalesOrderItems
                .Where(i => !i.IsDeleted && !i.ProductVariant.IsByproduct)
                .ToList();
            var varieties = riceItems
                .Where(i => i.ProductVariant.RiceVarietyId.HasValue)
                .Select(i => i.ProductVariant.RiceVariety)
                .Where(v => v != null)
                .GroupBy(v => v!.Id)
                .Select(g => g.First()!)
                .ToList();
            var singleVariety = varieties.Count == 1 ? varieties[0] : null;
            var totalRiceRequiredKg = riceItems.Sum(i => i.QuantityOrdered);
            var allocatedMillingRiceKg = so.MillingOrders
                .Where(o => !o.IsDeleted && o.Status?.Code != "CANCELLED")
                .Sum(o => o.TotalRiceOutputKg);
            var productNames = riceItems
                .Where(i => singleVariety != null && i.ProductVariant.RiceVarietyId == singleVariety.Id)
                .Select(i => i.ProductVariant.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();
            var varietyDisplayName = singleVariety == null
                ? null
                : !string.IsNullOrWhiteSpace(singleVariety.Name) &&
                  !singleVariety.Name.StartsWith("Giống #", StringComparison.OrdinalIgnoreCase)
                    ? singleVariety.Name
                    : productNames.Count > 0
                        ? string.Join(", ", productNames)
                        : singleVariety.Code;

            return new SalesOrderListDto
            {
            Id                   = so.Id,
            SOCode               = so.SOCode,
            CustomerId           = so.CustomerId,
            CustomerName         = so.Customer?.Name ?? "",
            StatusId             = so.StatusId,
            StatusName           = so.Status?.Name ?? "",
            StatusCode           = so.Status?.Code ?? "",
            StatusColor          = so.Status?.Color ?? "",
            Channel              = so.Channel,
            WarehouseId          = so.WarehouseId,
            WarehouseName        = so.Warehouse?.Name,
            OrderDate            = so.OrderDate,
            ExpectedDeliveryDate = so.ExpectedDeliveryDate,
            RequiresMilling      = so.RequiresMilling,
            RiceVarietyId        = singleVariety?.Id,
            RiceVarietyCode      = singleVariety?.Code,
            RiceVarietyName      = singleVariety?.Name,
            RiceVarietyDisplayName = varietyDisplayName,
            RiceVarietyCount     = varieties.Count,
            HasUnconfiguredRiceVariety = riceItems.Any(i => !i.ProductVariant.RiceVarietyId.HasValue),
            TotalRiceRequiredKg = totalRiceRequiredKg,
            AllocatedMillingRiceKg = allocatedMillingRiceKg,
            RemainingMillingRiceKg = Math.Max(0, totalRiceRequiredKg - allocatedMillingRiceKg),
            TotalAmount          = so.TotalAmount,
            DepositAmount        = so.DepositAmount,
            Note                 = so.Note,
            CancelReason         = so.CancelReason,
            CreatedDate          = so.CreatedDate
            };
        }).ToList();

        return ApiResponse.Success(new { Total = total, Items = dtos });
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        return ApiResponse.Success(MapDetail(so));
    }

    // ── Commands ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> CreateAsync(CreateSalesOrderDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            return ApiResponse.BadRequest("Đơn bán phải có ít nhất 1 dòng sản phẩm.", ApiCodeConstants.SalesOrder.InvalidRequest);

        var duplicateItems = dto.Items.GroupBy(x => x.ProductVariantId).FirstOrDefault(g => g.Count() > 1);
        if (duplicateItems != null)
            return ApiResponse.BadRequest("Không được thêm trùng cùng một biến thể sản phẩm (SKU) trên đơn bán.", ApiCodeConstants.SalesOrder.InvalidRequest);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        var orgId  = await GetCurrentOrganizationIdAsync();

        // Generate SOCode: SO-YYYYMMDD-XXXX
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"SO-{datePart}";
        var count    = await _salesOrderRepository
            .FindByCondition(x => x.SOCode.StartsWith(baseCode))
            .CountAsync();
        var soCode = $"{baseCode}-{(count + 1):D4}";

        // L2: Tránh race condition trùng mã khi nhiều request chạy đồng thời
        int attempts = 0;
        while (await _salesOrderRepository.AnyAsync(x => x.SOCode == soCode && x.OrganizationId == orgId) && attempts < 10)
        {
            attempts++;
            soCode = $"{baseCode}-{(count + 1 + attempts):D4}";
        }

        var statusId = await GetStatusIdAsync(SalesOrderStatusNames.New);

        // BE tự tính LineAmount và TotalAmount — không nhận từ frontend
        var items      = new List<SalesOrderItem>();
        decimal total  = 0;

        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityOrdered <= 0)
                return ApiResponse.BadRequest($"Số lượng sản phẩm {itemDto.ProductVariantId} phải > 0.", ApiCodeConstants.SalesOrder.InvalidRequest);

            var lineAmount = itemDto.QuantityOrdered * itemDto.UnitSalePrice - itemDto.DiscountAmount;
            if (lineAmount < 0) lineAmount = 0;

            total += lineAmount;
            items.Add(new SalesOrderItem
            {
                ProductVariantId = itemDto.ProductVariantId,
                QuantityOrdered  = itemDto.QuantityOrdered,
                UnitSalePrice    = itemDto.UnitSalePrice,
                DiscountAmount   = itemDto.DiscountAmount,
                LineAmount       = lineAmount,
                Note             = itemDto.Note,
                CreatedDate      = now,
                CreatedBy        = userId
            });
        }

        var order = new SalesOrder
        {
            SOCode               = soCode,
            CustomerId           = dto.CustomerId,
            StatusId             = statusId,
            Channel              = dto.Channel,
            WarehouseId          = dto.WarehouseId,
            OrganizationId       = orgId,
            OrderDate            = now,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            RequiresMilling      = dto.RequiresMilling,
            TotalAmount          = total,
            DepositAmount        = dto.DepositAmount,
            ShippingAddress      = dto.ShippingAddress,
            Note                 = dto.Note,
            CreatedDate          = now,
            CreatedBy            = userId,
            SalesOrderItems      = items
        };

        await _salesOrderRepository.CreateAsync(order);
        await _salesOrderRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên bán hàng và Chủ kho: có đơn bán mới.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.SalesOrderCreated,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.SALES, CommonConstants.Role.OWNER } },
            new object[] { soCode },
            "/admin/sales-orders",
            userId);

        return ApiResponse.Created(new { Id = order.Id, SOCode = soCode, TotalAmount = total },
            "Tạo đơn bán thành công.");
    }

    public async Task<ApiResponse> UpdateAsync(UpdateSalesOrderDto dto)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(dto.Id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        // Chỉ cho sửa khi đang ở NEW
        if (so.Status?.Code != SalesOrderStatusNames.New)
            return ApiResponse.Conflict("Chỉ có thể chỉnh sửa đơn ở trạng thái Mới tạo.",
                ApiCodeConstants.SalesOrder.InvalidState);

        var now = DateTimeHelper.VietnamNow();
        so.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        so.ShippingAddress      = dto.ShippingAddress;
        so.DepositAmount        = dto.DepositAmount;
        so.Note                 = dto.Note;
        so.LastModifiedDate     = now;
        so.UpdatedBy            = dto.UpdatedBy;

        if (dto.Items.Any())
        {
            var duplicateItems = dto.Items.GroupBy(x => x.ProductVariantId).FirstOrDefault(g => g.Count() > 1);
            if (duplicateItems != null)
                return ApiResponse.BadRequest("Không được thêm trùng cùng một biến thể sản phẩm (SKU) trên đơn bán.", ApiCodeConstants.SalesOrder.InvalidRequest);

            // Xóa items cũ
            foreach (var old in so.SalesOrderItems.ToList())
            {
                old.IsDeleted = true;
                await _salesOrderItemRepository.UpdateAsync(old);
            }

            decimal total = 0;
            foreach (var itemDto in dto.Items)
            {
                var lineAmount = itemDto.QuantityOrdered * itemDto.UnitSalePrice - itemDto.DiscountAmount;
                if (lineAmount < 0) lineAmount = 0;
                total += lineAmount;

                await _salesOrderItemRepository.CreateAsync(new SalesOrderItem
                {
                    SalesOrderId     = so.Id,
                    ProductVariantId = itemDto.ProductVariantId,
                    QuantityOrdered  = itemDto.QuantityOrdered,
                    UnitSalePrice    = itemDto.UnitSalePrice,
                    DiscountAmount   = itemDto.DiscountAmount,
                    LineAmount       = lineAmount,
                    Note             = itemDto.Note,
                    CreatedDate      = now
                });
            }
            so.TotalAmount = total;
        }

        await _salesOrderRepository.UpdateAsync(so);
        await _salesOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Cập nhật đơn bán thành công.");
    }

    public async Task<ApiResponse> ConfirmAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        if (so.Status?.Code != SalesOrderStatusNames.New)
            return ApiResponse.Conflict(
                $"Đơn đang ở trạng thái '{so.Status?.Name}', không thể xác nhận.",
                ApiCodeConstants.SalesOrder.InvalidState);

        so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.PendingConfirm);
        so.LastModifiedDate = DateTimeHelper.VietnamNow();
        so.UpdatedBy        = GetCurrentUserId();

        await _salesOrderRepository.UpdateAsync(so);
        await _salesOrderRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên bán hàng và Nhân viên kho: đơn bán đã xác nhận, chuẩn bị hàng.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.SalesOrderConfirmed,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.SALES, CommonConstants.Role.WAREHOUSE } },
            new object[] { so.SOCode },
            "/admin/sales-orders",
            GetCurrentUserId());

        return ApiResponse.Success(message: "Đơn bán đã được xác nhận.");
    }

    public async Task<ApiResponse> ReserveAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.Error("Không tìm thấy đơn bán.", 404, ApiCodeConstants.SalesOrder.NotFound);

        if (so.Status?.Code != SalesOrderStatusNames.PendingConfirm)
            return ApiResponse.Error(
                $"Đơn đang ở trạng thái '{so.Status?.Name}', chỉ có thể giữ hàng khi ở Chờ xác nhận.",
                409, ApiCodeConstants.SalesOrder.InvalidState);

        // Gap 2: Đơn "cần xay" (RequiresMilling) bắt buộc phải có ít nhất 1 lệnh xay ĐÃ HOÀN THÀNH
        // gắn với đơn này trước khi giữ hàng — để đảm bảo gạo thành phẩm đã thực sự được sản xuất cho đơn.
        if (so.RequiresMilling)
        {
            var hasCompletedMilling = await _millingOrderRepository
                .FindByCondition(x => x.SalesOrderId == so.Id && !x.IsDeleted, false, x => x.Status)
                .AnyAsync(x => x.Status != null && x.Status.Code == LookupCodes.MillingOrderStatus.Completed);

            if (!hasCompletedMilling)
                return ApiResponse.Error(
                    "Đơn hàng yêu cầu xay xát: vui lòng tạo và hoàn thành lệnh xay cho đơn này trước khi giữ hàng.",
                    422, ApiCodeConstants.SalesOrder.InvalidState);
        }

        // 1. Kiểm tra khách hàng còn hoạt động
        var customer = await _customerRepository.GetByIdAsync(so.CustomerId);
        if (customer == null || customer.IsDeleted || !customer.IsActive)
            return ApiResponse.Error("Khách hàng không còn hoạt động.", 422, ApiCodeConstants.SalesOrder.InvalidRequest);

        // 2. Kiểm tra hạn mức công nợ
        var existingDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
            !x.IsDeleted &&
            x.PartyType == "CUSTOMER" &&
            x.PartyId == so.CustomerId &&
            x.Direction == "RECEIVABLE" &&
            x.IsActive);

        if (existingDebt != null && existingDebt.CreditLimit.HasValue)
        {
            var remainingAfterOrder = existingDebt.CurrentBalance + so.TotalAmount - (so.DepositAmount ?? 0);
            if (remainingAfterOrder > existingDebt.CreditLimit.Value)
                return ApiResponse.Error(
                    $"Vượt hạn mức công nợ. Hạn mức: {existingDebt.CreditLimit:N0} VNĐ, " +
                    $"Dư nợ sau đơn: {remainingAfterOrder:N0} VNĐ.",
                    422, ApiCodeConstants.SalesOrder.CreditLimitExceeded);
        }

        if (!so.WarehouseId.HasValue)
            return ApiResponse.Error("Đơn bán chưa có kho xuất.", 400, ApiCodeConstants.SalesOrder.InvalidRequest);

        await using var tx = await _salesOrderRepository.BeginTransactionAsync();
        try
        {
            var now = DateTimeHelper.VietnamNow();
            var userId = GetCurrentUserId();

            // 3. Khóa tồn vật lý (FIFO)
            foreach (var item in so.SalesOrderItems.Where(i => !i.IsDeleted))
            {
                var availableRows = await _inventoryRepository.GetAvailableForSalesAsync(
                    item.ProductVariantId, so.WarehouseId.Value);

                var totalAvail = availableRows.Sum(x => x.QuantityOnHand - x.QuantityReserved);

                if (totalAvail < item.QuantityOrdered)
                    return ApiResponse.Error(
                        $"Tồn khả dụng không đủ cho sản phẩm ID {item.ProductVariantId}. " +
                        $"Cần: {item.QuantityOrdered}, Khả dụng: {totalAvail}.",
                        422, ApiCodeConstants.SalesOrder.InsufficientStock);

                decimal remainingToReserve = item.QuantityOrdered;

                foreach (var inv in availableRows.OrderBy(x => x.CreatedDate))
                {
                    if (remainingToReserve <= 0) break;

                    var availInRow = inv.QuantityOnHand - inv.QuantityReserved;
                    if (availInRow <= 0) continue;

                    var take = Math.Min(availInRow, remainingToReserve);

                    var before = inv.QuantityReserved;
                    inv.QuantityReserved += take;
                    inv.LastModifiedDate = now;
                    await _inventoryRepository.UpdateAsync(inv);

                    // Track reservation using InventoryTransaction
                    var invTx = new InventoryTransaction
                    {
                        InventoryId = inv.Id,
                        WarehouseId = inv.WarehouseId,
                        LocationId = inv.LocationId,
                        ProductVariantId = inv.ProductVariantId,
                        PaddyLotId = inv.PaddyLotId,
                        TransactionType = InventoryTransactionTypeConstants.Reserve,
                        ReferenceType = InventoryReferenceTypeConstants.SalesOrder,
                        ReferenceId = so.Id,
                        ReferenceItemId = item.Id,
                        Quantity = take,
                        BeforeQuantity = before, // Track reserved quantity before
                        AfterQuantity = inv.QuantityReserved, // Track reserved quantity after
                        WeightKg = take,
                        Note = $"Khóa tồn cho đơn bán {so.SOCode}",
                        CreatedDate = now,
                        CreatedBy = userId
                    };
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(invTx);

                    remainingToReserve -= take;
                }
            }

            // 4. Chuyển trạng thái sang RESERVED
            so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Reserved);
            so.LastModifiedDate = now;
            so.UpdatedBy        = userId;
            await _salesOrderRepository.UpdateAsync(so);

            await _salesOrderRepository.SaveChangesAsync();
            await _salesOrderRepository.EndTransactionAsync();

            return ApiResponse.Success(message: "Đã giữ hàng thành công.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _salesOrderRepository.RollbackTransactionAsync();
            return ApiResponse.Error(
                "Tồn kho đã thay đổi trong lúc xử lý. Vui lòng thử lại.",
                409, ApiCodeConstants.SalesOrder.ConcurrencyConflict);
        }
        catch
        {
            await _salesOrderRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> CancelAsync(int id, string? reason = null)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.Error("Không tìm thấy đơn bán.", 404, ApiCodeConstants.SalesOrder.NotFound);

        var allowedStates = new[]
        {
            SalesOrderStatusNames.New,
            SalesOrderStatusNames.PendingConfirm,
            SalesOrderStatusNames.Reserved,
            SalesOrderStatusNames.Preparing
        };
        if (!allowedStates.Contains(so.Status?.Code))
            return ApiResponse.Error(
                $"Không thể hủy đơn ở trạng thái '{so.Status?.Name}'.",
                409, ApiCodeConstants.SalesOrder.InvalidState);

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        await using var tx = await _salesOrderRepository.BeginTransactionAsync();
        try
        {
            // Kiểm tra OutboundOrder
            var outbounds = await _outboundOrderRepository
                .FindByCondition(x => x.SalesOrderId == id && !x.IsDeleted, false, x => x.OutboundOrderStatus)
                .ToListAsync();

            if (outbounds.Any(x => x.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Cancelled && x.OutboundOrderStatus?.Code != OutboundOrderStatusNames.Draft))
                return ApiResponse.Error("Không thể hủy đơn bán vì đã có Phiếu xuất đang xử lý. Vui lòng hủy phiếu xuất trước.", 409, ApiCodeConstants.SalesOrder.InvalidState);

            // Xóa OutboundOrder DRAFT
            foreach (var draft in outbounds.Where(x => x.OutboundOrderStatus?.Code == OutboundOrderStatusNames.Draft))
            {
                draft.IsDeleted = true;
                draft.LastModifiedDate = now;
                draft.UpdatedBy = userId;
                await _outboundOrderRepository.UpdateAsync(draft);
            }

            // Giải phóng QuantityReserved nếu đang RESERVED hoặc PREPARING
            if (so.Status?.Code == SalesOrderStatusNames.Reserved || so.Status?.Code == SalesOrderStatusNames.Preparing)
            {
                var reserveTxs = await _inventoryTransactionRepository
                    .FindByCondition(x => x.ReferenceType == InventoryReferenceTypeConstants.SalesOrder 
                                       && x.ReferenceId == id 
                                       && x.TransactionType == InventoryTransactionTypeConstants.Reserve)
                    .ToListAsync();

                // Nạp 1 lượt các Inventory liên quan (thay cho GetById trong vòng lặp -> tránh N+1).
                // Dùng chung instance theo Id nên các lần trừ QuantityReserved vẫn cộng dồn đúng như cũ.
                var reserveInvIds = reserveTxs.Select(x => x.InventoryId).Distinct().ToList();
                var reserveInvMap = (await _inventoryRepository
                        .FindByCondition(i => reserveInvIds.Contains(i.Id))
                        .ToListAsync())
                    .ToDictionary(i => i.Id);

                foreach (var rx in reserveTxs)
                {
                    if (reserveInvMap.TryGetValue(rx.InventoryId, out var inv) && inv != null)
                    {
                        var before = inv.QuantityReserved;
                        inv.QuantityReserved = Math.Max(0, inv.QuantityReserved - rx.Quantity);
                        inv.LastModifiedDate = now;
                        await _inventoryRepository.UpdateAsync(inv);

                        var unreserveTx = new InventoryTransaction
                        {
                            InventoryId = inv.Id,
                            WarehouseId = inv.WarehouseId,
                            LocationId = inv.LocationId,
                            ProductVariantId = inv.ProductVariantId,
                            PaddyLotId = inv.PaddyLotId,
                            TransactionType = InventoryTransactionTypeConstants.ReleaseReserve,
                            ReferenceType = InventoryReferenceTypeConstants.SalesOrder,
                            ReferenceId = so.Id,
                            ReferenceItemId = rx.ReferenceItemId,
                            Quantity = -rx.Quantity,
                            BeforeQuantity = before,
                            AfterQuantity = inv.QuantityReserved,
                            WeightKg = -rx.Quantity,
                            Note = $"Giải phóng tồn do hủy đơn bán {so.SOCode}",
                            CreatedDate = now,
                            CreatedBy = userId
                        };
                        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(unreserveTx);
                    }
                }
            }

            so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Cancelled);
            var trimmedReason   = reason?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedReason))
            {
                so.CancelReason = trimmedReason.Length > 500
                    ? trimmedReason.Substring(0, 500)
                    : trimmedReason;
            }
            so.LastModifiedDate = now;
            so.UpdatedBy        = userId;
            await _salesOrderRepository.UpdateAsync(so);

            await _salesOrderRepository.SaveChangesAsync();
            await _salesOrderRepository.EndTransactionAsync();

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.SalesOrderCancelled,
                new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.SALES, CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE } },
                new object[] { so.SOCode },
                "/admin/sales-orders",
                GetCurrentUserId());

            return ApiResponse.Success(message: "Đơn bán đã được hủy.");
        }
        catch
        {
            await _salesOrderRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> CreateOutboundAsync(int id, CreateOutboundDto dto)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.Error("Không tìm thấy đơn bán.", 404, ApiCodeConstants.SalesOrder.NotFound);

        var allowedStates = new[]
        {
            SalesOrderStatusNames.Reserved,
            SalesOrderStatusNames.Preparing
        };
        if (!allowedStates.Contains(so.Status?.Code))
            return ApiResponse.Error(
                $"Không thể tạo phiếu xuất từ đơn ở trạng thái '{so.Status?.Name}'.",
                409, ApiCodeConstants.SalesOrder.InvalidState);

        var draftStatus = await _outboundOrderStatusRepository.FirstOrDefaultAsync(
            x => x.Code == OutboundOrderStatusNames.Draft && !x.IsDeleted);
        var draftStatusId = draftStatus?.Id
            ?? throw new InvalidOperationException("OutboundOrderStatus DRAFT not found.");

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        await using var tx = await _salesOrderRepository.BeginTransactionAsync();
        try
        {
            var outbound = new OutboundOrder
            {
                SalesOrderId          = so.Id,
                WarehouseId           = so.WarehouseId ?? 0,
                OrganizationId        = so.OrganizationId,
                OutboundOrderStatusId = draftStatusId,
                TotalDispatchedValue  = 0,
                TotalDispatchedSaleValue = 0,
                Note                  = $"Tạo từ đơn bán {so.SOCode}",
                CreatedDate           = now,
                CreatedBy             = userId
            };

            // Calculate already dispatched or currently drafting quantities
            var existingOutbounds = await _outboundOrderRepository
                .FindByCondition(x => x.SalesOrderId == id && !x.IsDeleted && 
                                      x.OutboundOrderStatus != null && 
                                      x.OutboundOrderStatus.Code != OutboundOrderStatusNames.Cancelled,
                                      false, x => x.OutboundOrderItems)
                .ToListAsync();

            foreach (var itemDto in dto.Items)
            {
                var soItem = so.SalesOrderItems.FirstOrDefault(x => x.ProductVariantId == itemDto.ProductVariantId && !x.IsDeleted);
                if (soItem == null) 
                    return ApiResponse.Error($"Sản phẩm ID {itemDto.ProductVariantId} không có trong đơn bán.", 400);

                var alreadyAssigned = existingOutbounds.SelectMany(x => x.OutboundOrderItems)
                                                       .Where(x => x.SalesOrderItemId == soItem.Id)
                                                       .Sum(x => x.QuantityOrdered);

                if (alreadyAssigned + itemDto.QuantityToDispatch > soItem.QuantityOrdered)
                    return ApiResponse.Error(
                        $"Số lượng xuất ({itemDto.QuantityToDispatch}) vượt quá số lượng còn lại " +
                        $"({soItem.QuantityOrdered - alreadyAssigned}) của sản phẩm ID {itemDto.ProductVariantId}.", 422);

                outbound.OutboundOrderItems.Add(new OutboundOrderItem
                {
                    ProductVariantId = itemDto.ProductVariantId,
                    QuantityOrdered  = itemDto.QuantityToDispatch,
                    QuantityPicked   = 0,
                    UnitCostPrice    = 0,
                    SalesOrderItemId = soItem.Id,
                    CreatedDate      = now,
                    CreatedBy        = userId
                });
            }

            if (!outbound.OutboundOrderItems.Any())
                return ApiResponse.Error("Phải có ít nhất 1 sản phẩm để xuất kho.", 400);

            await _outboundOrderRepository.CreateAsync(outbound);

            // Chuyển SalesOrder → PREPARING
            if (so.Status?.Code == SalesOrderStatusNames.Reserved)
            {
                so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Preparing);
                so.LastModifiedDate = now;
                so.UpdatedBy        = userId;
                await _salesOrderRepository.UpdateAsync(so);
            }

            await _salesOrderRepository.SaveChangesAsync();
            await _salesOrderRepository.EndTransactionAsync();

            return ApiResponse.Created(new { OutboundOrderId = outbound.Id },
                "Tạo phiếu xuất kho thành công.");
        }
        catch
        {
            await _salesOrderRepository.RollbackTransactionAsync();
            throw;
        }
    }
}

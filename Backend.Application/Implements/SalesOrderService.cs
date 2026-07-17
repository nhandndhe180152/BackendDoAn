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
    private readonly IPartyDebtRepository _partyDebtRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SalesOrderService(
        ISalesOrderRepository salesOrderRepository,
        IRepositoryBase<SalesOrderItem, int> salesOrderItemRepository,
        IRepositoryBase<SalesOrderStatus, int> salesOrderStatusRepository,
        IRepositoryBase<OutboundOrder, int> outboundOrderRepository,
        IRepositoryBase<OutboundOrderItem, int> outboundOrderItemRepository,
        IRepositoryBase<OutboundOrderStatus, int> outboundOrderStatusRepository,
        IRepositoryBase<Customer, int> customerRepository,
        IInventoryRepository inventoryRepository,
        IPartyDebtRepository partyDebtRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _salesOrderRepository        = salesOrderRepository;
        _salesOrderItemRepository    = salesOrderItemRepository;
        _salesOrderStatusRepository  = salesOrderStatusRepository;
        _outboundOrderRepository     = outboundOrderRepository;
        _outboundOrderItemRepository = outboundOrderItemRepository;
        _outboundOrderStatusRepository = outboundOrderStatusRepository;
        _customerRepository          = customerRepository;
        _inventoryRepository         = inventoryRepository;
        _partyDebtRepository         = partyDebtRepository;
        _httpContextAccessor         = httpContextAccessor;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;

    private async Task<int> GetStatusIdAsync(string name)
    {
        var status = await _salesOrderStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return status?.Id ?? throw new InvalidOperationException($"SalesOrderStatus '{name}' not found.");
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
                TotalDispatchedValue = o.TotalDispatchedValue,
                CompletedDate        = o.CompletedDate
            }).ToList()
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetPagedAsync(SalesOrderPagedQuery query)
    {
        var skip = (query.Page - 1) * query.PageSize;
        var total = await _salesOrderRepository.CountAsync(query.Keyword);
        var list = await _salesOrderRepository.GetPagedListAsync(query.Keyword, skip, query.PageSize);

        var dtos = list.Select(so => new SalesOrderListDto
        {
            Id                   = so.Id,
            SOCode               = so.SOCode,
            CustomerId           = so.CustomerId,
            CustomerName         = so.Customer?.Name ?? "",
            StatusId             = so.StatusId,
            StatusName           = so.Status?.Name ?? "",
            StatusColor          = so.Status?.Color ?? "",
            Channel              = so.Channel,
            WarehouseId          = so.WarehouseId,
            WarehouseName        = so.Warehouse?.Name,
            OrderDate            = so.OrderDate,
            ExpectedDeliveryDate = so.ExpectedDeliveryDate,
            RequiresMilling      = so.RequiresMilling,
            TotalAmount          = so.TotalAmount,
            DepositAmount        = so.DepositAmount,
            Note                 = so.Note,
            CreatedDate          = so.CreatedDate
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

        var now = DateTimeHelper.VietnamNow();

        // Generate SOCode: SO-YYYYMMDD-XXXX
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"SO-{datePart}";
        var count    = await _salesOrderRepository
            .FindByCondition(x => x.SOCode.StartsWith(baseCode))
            .CountAsync();
        var soCode = $"{baseCode}-{(count + 1):D4}";

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
                CreatedBy        = dto.CreatedBy
            });
        }

        var order = new SalesOrder
        {
            SOCode               = soCode,
            CustomerId           = dto.CustomerId,
            StatusId             = statusId,
            Channel              = dto.Channel,
            WarehouseId          = dto.WarehouseId,
            OrganizationId       = dto.OrganizationId,
            OrderDate            = now,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            RequiresMilling      = dto.RequiresMilling,
            TotalAmount          = total,
            DepositAmount        = dto.DepositAmount,
            ShippingAddress      = dto.ShippingAddress,
            Note                 = dto.Note,
            CreatedDate          = now,
            CreatedBy            = dto.CreatedBy,
            SalesOrderItems      = items
        };

        await _salesOrderRepository.CreateAsync(order);
        await _salesOrderRepository.SaveChangesAsync();

        return ApiResponse.Created(new { Id = order.Id, SOCode = soCode, TotalAmount = total },
            "Tạo đơn bán thành công.");
    }

    public async Task<ApiResponse> UpdateAsync(UpdateSalesOrderDto dto)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(dto.Id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        // Chỉ cho sửa khi đang ở NEW
        if (so.Status?.Name != SalesOrderStatusNames.New)
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

        if (so.Status?.Name != SalesOrderStatusNames.New)
            return ApiResponse.Conflict(
                $"Đơn đang ở trạng thái '{so.Status?.Name}', không thể xác nhận.",
                ApiCodeConstants.SalesOrder.InvalidState);

        so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.PendingConfirm);
        so.LastModifiedDate = DateTimeHelper.VietnamNow();
        so.UpdatedBy        = GetCurrentUserId();

        await _salesOrderRepository.UpdateAsync(so);
        await _salesOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(message: "Đơn bán đã được xác nhận.");
    }

    public async Task<ApiResponse> ReserveAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        if (so.Status?.Name != SalesOrderStatusNames.PendingConfirm)
            return ApiResponse.Conflict(
                $"Đơn đang ở trạng thái '{so.Status?.Name}', chỉ có thể giữ hàng khi ở Chờ xác nhận.",
                ApiCodeConstants.SalesOrder.InvalidState);

        // 1. Kiểm tra khách hàng còn hoạt động
        var customer = await _customerRepository.GetByIdAsync(so.CustomerId);
        if (customer == null || customer.IsDeleted || !customer.IsActive)
            return ApiResponse.UnprocessableEntity("Khách hàng không còn hoạt động.",
                ApiCodeConstants.SalesOrder.InvalidRequest);

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
                return ApiResponse.UnprocessableEntity(
                    $"Vượt hạn mức công nợ. Hạn mức: {existingDebt.CreditLimit:N0} VNĐ, " +
                    $"Dư nợ sau đơn: {remainingAfterOrder:N0} VNĐ.",
                    ApiCodeConstants.SalesOrder.CreditLimitExceeded);
        }

        // 3. Kiểm tra + giữ tồn khả dụng cho từng sản phẩm
        if (!so.WarehouseId.HasValue)
            return ApiResponse.BadRequest("Đơn bán chưa có kho xuất.", ApiCodeConstants.SalesOrder.InvalidRequest);

        var reservedInventories = new List<(Inventory inv, decimal qty)>();

        foreach (var item in so.SalesOrderItems.Where(i => !i.IsDeleted))
        {
            var available = await _inventoryRepository.GetAvailableForSalesAsync(
                item.ProductVariantId, so.WarehouseId.Value);

            // Kiểm tra lot cách ly
            var quarantinedLot = available.FirstOrDefault(x =>
                x.PaddyLot != null && !x.PaddyLot.Status.IsSellable);
            if (quarantinedLot != null)
                return ApiResponse.UnprocessableEntity(
                    $"Lô {quarantinedLot.PaddyLot?.LotCode} đang bị cách ly, không thể giữ hàng.",
                    ApiCodeConstants.SalesOrder.LotQuarantined);

            decimal needed = item.QuantityOrdered;
            decimal totalAvail = available.Sum(x => x.QuantityOnHand - x.QuantityReserved);

            if (totalAvail < needed)
                return ApiResponse.UnprocessableEntity(
                    $"Tồn khả dụng không đủ cho sản phẩm ID {item.ProductVariantId}. " +
                    $"Cần: {needed}, Khả dụng: {totalAvail}.",
                    ApiCodeConstants.SalesOrder.InsufficientStock);

            // FIFO: phân bổ tồn từ từng lô
            foreach (var inv in available)
            {
                if (needed <= 0) break;
                var avail = inv.QuantityOnHand - inv.QuantityReserved;
                var take  = Math.Min(avail, needed);
                reservedInventories.Add((inv, take));
                needed -= take;
            }
        }

        // 4. Tăng QuantityReserved (chưa giảm QuantityOnHand)
        try
        {
            foreach (var (inv, qty) in reservedInventories)
            {
                inv.QuantityReserved  += qty;
                inv.LastModifiedDate   = DateTimeHelper.VietnamNow();
                await _inventoryRepository.UpdateAsync(inv);
            }

            so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Reserved);
            so.LastModifiedDate = DateTimeHelper.VietnamNow();
            so.UpdatedBy        = GetCurrentUserId();
            await _salesOrderRepository.UpdateAsync(so);
            await _salesOrderRepository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse.Conflict(
                "Tồn kho đã thay đổi trong lúc xử lý. Vui lòng thử lại.",
                ApiCodeConstants.SalesOrder.ConcurrencyConflict);
        }

        return ApiResponse.Success(message: "Đã giữ hàng thành công.");
    }

    public async Task<ApiResponse> CancelAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        var allowedStates = new[]
        {
            SalesOrderStatusNames.New,
            SalesOrderStatusNames.PendingConfirm,
            SalesOrderStatusNames.Reserved
        };
        if (!allowedStates.Contains(so.Status?.Name))
            return ApiResponse.Conflict(
                $"Không thể hủy đơn ở trạng thái '{so.Status?.Name}'.",
                ApiCodeConstants.SalesOrder.InvalidState);

        // Nếu đã giữ hàng → phải hoàn trả QuantityReserved
        if (so.Status?.Name == SalesOrderStatusNames.Reserved && so.WarehouseId.HasValue)
        {
            foreach (var item in so.SalesOrderItems.Where(i => !i.IsDeleted))
            {
                var available = await _inventoryRepository.GetAvailableForSalesAsync(
                    item.ProductVariantId, so.WarehouseId.Value);

                // Lấy các inventory đang reserved (đơn giản: release theo tổng qty)
                decimal toRelease = item.QuantityOrdered;
                foreach (var inv in available.OrderByDescending(x => x.QuantityReserved))
                {
                    if (toRelease <= 0) break;
                    var release = Math.Min(inv.QuantityReserved, toRelease);
                    inv.QuantityReserved -= release;
                    toRelease -= release;
                    inv.LastModifiedDate = DateTimeHelper.VietnamNow();
                    await _inventoryRepository.UpdateAsync(inv);
                }
            }
        }

        so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Cancelled);
        so.LastModifiedDate = DateTimeHelper.VietnamNow();
        so.UpdatedBy        = GetCurrentUserId();
        await _salesOrderRepository.UpdateAsync(so);
        await _salesOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(message: "Đơn bán đã được hủy.");
    }

    public async Task<ApiResponse> CreateOutboundAsync(int id)
    {
        var so = await _salesOrderRepository.GetByIdDetailAsync(id);
        if (so == null || so.IsDeleted)
            return ApiResponse.NotFound("Không tìm thấy đơn bán.", ApiCodeConstants.SalesOrder.NotFound);

        var allowedStates = new[]
        {
            SalesOrderStatusNames.Reserved,
            SalesOrderStatusNames.Preparing
        };
        if (!allowedStates.Contains(so.Status?.Name))
            return ApiResponse.Conflict(
                $"Không thể tạo phiếu xuất từ đơn ở trạng thái '{so.Status?.Name}'.",
                ApiCodeConstants.SalesOrder.InvalidState);

        var draftStatusId = await _outboundOrderStatusRepository.FirstOrDefaultAsync(
            x => x.Name == OutboundOrderStatusNames.Draft && !x.IsDeleted)
            .ContinueWith(t => t.Result?.Id ?? throw new InvalidOperationException("OutboundOrderStatus DRAFT not found."));

        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        var outbound = new OutboundOrder
        {
            SalesOrderId          = so.Id,
            WarehouseId           = so.WarehouseId ?? 0,
            OrganizationId        = so.OrganizationId,
            OutboundOrderStatusId = draftStatusId,
            TotalDispatchedValue  = 0,
            Note                  = $"Tạo từ đơn bán {so.SOCode}",
            CreatedDate           = now,
            CreatedBy             = userId
        };

        // Tạo OutboundOrderItem từ SalesOrderItem
        foreach (var item in so.SalesOrderItems.Where(i => !i.IsDeleted))
        {
            outbound.OutboundOrderItems.Add(new OutboundOrderItem
            {
                ProductVariantId = item.ProductVariantId,
                QuantityOrdered  = item.QuantityOrdered,
                QuantityPicked   = 0,
                UnitCostPrice    = 0,    // Sẽ được cập nhật khi allocate
                SalesOrderItemId = item.Id,
                CreatedDate      = now,
                CreatedBy        = userId
            });
        }

        await _outboundOrderRepository.CreateAsync(outbound);

        // Chuyển SalesOrder → PREPARING (nếu chưa)
        if (so.Status?.Name == SalesOrderStatusNames.Reserved)
        {
            so.StatusId         = await GetStatusIdAsync(SalesOrderStatusNames.Preparing);
            so.LastModifiedDate = now;
            so.UpdatedBy        = userId;
            await _salesOrderRepository.UpdateAsync(so);
        }

        await _salesOrderRepository.SaveChangesAsync();

        return ApiResponse.Created(new { OutboundOrderId = outbound.Id },
            "Tạo phiếu xuất kho thành công.");
    }
}

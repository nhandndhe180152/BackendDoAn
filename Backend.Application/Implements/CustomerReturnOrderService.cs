using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Backend.Application.DTOs.CustomerReturns;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Share.Services;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Domain.Entities;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class CustomerReturnOrderService : ICustomerReturnOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CustomerReturnOrderService> _logger;

    private readonly IScheduledJobService? _scheduledJobService;
    private readonly IPaddyLotBagInvariantService? _bagInvariantService;

    public CustomerReturnOrderService(
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CustomerReturnOrderService> logger,
        IScheduledJobService? scheduledJobService = null,
        IPaddyLotBagInvariantService? bagInvariantService = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _scheduledJobService = scheduledJobService;
        _bagInvariantService = bagInvariantService;
    }

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 1;

    private async Task<int> GetCurrentOrganizationIdAsync()
    {
        var officeIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimNames.OFFICE_ID)?.Value;
        if (int.TryParse(officeIdStr, out var id) && id > 0)
        {
            return id;
        }

        var defaultOrg = await _context.Organizations
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

        return defaultOrg?.Id 
            ?? throw new InvalidOperationException("Không tìm thấy tổ chức hoạt động nào trong hệ thống.");
    }

    private async Task<bool> CheckPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = await _context.UserRoles
            .Include(x => x.Role)
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .Select(x => x.Role.Code)
            .ToListAsync(cancellationToken);

        if (roles.Contains(LookupCodes.Role.Admin)) return true;

        if (action == LookupCodes.Action.Approve || action == LookupCodes.Action.Confirm || action == LookupCodes.Action.Cancel || action == LookupCodes.Action.Preview || action == "REJECT" || action == "REFUND")
        {
            return roles.Contains(LookupCodes.Role.WarehouseOwner);
        }
        if (action == LookupCodes.Action.Inspect || action == "RECEIVE")
        {
            return roles.Contains(LookupCodes.Role.WarehouseStaff);
        }
        if (action == LookupCodes.Action.Create && roles.Contains(LookupCodes.Role.EndUser))
        {
            return true;
        }
        if (action == LookupCodes.Action.Create || action == LookupCodes.Action.Update || action == "SUBMIT")
        {
            return roles.Contains(LookupCodes.Role.SalesStaff) || roles.Contains(LookupCodes.Role.WarehouseOwner);
        }

        return false;
    }

    public async Task<ApiResponse> CreateAsync(CreateCustomerReturnOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("CREATE", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        if (dto.WarehouseId <= 0 || dto.CustomerId <= 0)
            return ApiResponse.BadRequest(message: "Kho tiếp nhận và khách hàng là bắt buộc.");

        if (dto.Items == null || dto.Items.Count == 0)
            return ApiResponse.BadRequest(message: "Phiếu trả hàng phải có ít nhất một mặt hàng.");

        if (!dto.OutboundOrderId.HasValue)
            return ApiResponse.BadRequest(message: "Phiếu xuất gốc là bắt buộc đối với đơn trả hàng.", code: "CUSTOMER_RETURN_SOURCE_REQUIRED");

        var orgId = await GetCurrentOrganizationIdAsync();

        var allocationKeys = dto.Items
            .SelectMany(x => x.Allocations ?? new List<CreateCustomerReturnOrderItemAllocationDto>())
            .Select(x => x.OutboundOrderItemAllocationId.HasValue
                ? $"OUT-{x.OutboundOrderItemAllocationId.Value}"
                : x.PaddyLotId.HasValue ? $"LOT-{x.PaddyLotId.Value}" : "INVALID")
            .ToList();
        if (allocationKeys.Contains("INVALID") || allocationKeys.Distinct().Count() != allocationKeys.Count)
            return ApiResponse.BadRequest(message: "Mỗi phân bổ xuất hoặc lô hàng chỉ được khai báo một lần trong phiếu trả.");

        var warehouseExists = await _context.Warehouses
            .AnyAsync(x => x.Id == dto.WarehouseId && x.IsActive && !x.IsDeleted, cancellationToken);
        if (!warehouseExists)
            return ApiResponse.BadRequest(message: "Kho tiếp nhận không tồn tại hoặc đã ngừng hoạt động.");

        var customerExists = await _context.Customers
            // Dữ liệu cũ có Customer.OrganizationId = NULL. Quyền tenant vẫn được bảo vệ
            // bởi phiếu xuất/đơn bán nguồn và kiểm tra CustomerId ngay bên dưới.
            .AnyAsync(x => x.Id == dto.CustomerId
                && (x.OrganizationId == orgId || x.OrganizationId == null)
                && x.IsActive
                && !x.IsDeleted, cancellationToken);
        if (!customerExists)
            return ApiResponse.BadRequest(message: "Khách hàng không tồn tại hoặc đã ngừng hoạt động.");

        OutboundOrder? outbound = null;
        if (dto.OutboundOrderId.HasValue)
        {
            outbound = await _context.OutboundOrders
                .Include(o => o.OutboundOrderStatus)
                .Include(o => o.SalesOrder)
                .FirstOrDefaultAsync(o => o.Id == dto.OutboundOrderId.Value
                    && (o.OrganizationId == orgId || (o.OrganizationId == null && o.SalesOrder.OrganizationId == orgId))
                    && !o.IsDeleted, cancellationToken);

            if (outbound == null)
                return ApiResponse.NotFound(message: "Không tìm thấy phiếu xuất gốc.");

            // #19: Null-check tránh NullReferenceException (500) khi phiếu xuất thiếu trạng thái/đơn bán liên kết
            if (outbound.OutboundOrderStatus == null)
                return ApiResponse.UnprocessableEntity(message: "Phiếu xuất gốc chưa có trạng thái hợp lệ.");

            if (outbound.SalesOrder == null)
                return ApiResponse.UnprocessableEntity(message: "Phiếu xuất gốc không gắn với đơn bán nào.");

            if (outbound.OutboundOrderStatus.Code != OutboundOrderStatusNames.Dispatched && outbound.OutboundOrderStatus.Code != OutboundOrderStatusNames.Completed)
                return ApiResponse.UnprocessableEntity(message: "Chỉ được trả hàng đối với phiếu xuất đã Đang giao hàng hoặc Hoàn thành.");

            if (outbound.SalesOrder.CustomerId != dto.CustomerId)
                return ApiResponse.BadRequest(message: "Khách hàng không khớp với đơn xuất hàng gốc.");

            if (outbound.WarehouseId != dto.WarehouseId)
                return ApiResponse.BadRequest(message: "Kho tiếp nhận phải khớp với kho xuất hàng gốc.");
        }

        if (dto.CustomerFeedbackId.HasValue)
        {
            if (outbound == null)
                return ApiResponse.BadRequest(message: "Phiếu xuất gốc là bắt buộc khi tạo trả hàng từ khiếu nại.");

            var feedback = await _context.CustomerFeedbacks
                .Include(x => x.CustomerReturnOrder)
                .FirstOrDefaultAsync(x => x.Id == dto.CustomerFeedbackId.Value && !x.IsDeleted, cancellationToken);
            if (feedback == null)
                return ApiResponse.NotFound(message: "Không tìm thấy khiếu nại gốc.");
            if (feedback.OutboundOrderId != outbound.Id || feedback.SalesOrderId != outbound.SalesOrderId)
                return ApiResponse.BadRequest(message: "Khiếu nại không thuộc phiếu xuất/đơn bán đã chọn.");
            if (feedback.CustomerReturnOrder != null)
                return ApiResponse.Conflict(message: "Khiếu nại này đã có phiếu trả hàng.");
        }

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Draft && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái DRAFT cho đơn trả hàng.");

        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var baseCode = $"CRT-{datePart}";
        var cntToday = await _context.CustomerReturnOrders
            .CountAsync(x => x.ReturnCode.StartsWith(baseCode), cancellationToken);
        var returnCode = $"{baseCode}-{(cntToday + 1):D6}";

        // #5: Retry chống trùng mã khi nhiều request chạy đồng thời
        int codeAttempts = 0;
        while (await _context.CustomerReturnOrders.AnyAsync(x => x.ReturnCode == returnCode, cancellationToken) && codeAttempts < 10)
        {
            codeAttempts++;
            returnCode = $"{baseCode}-{(cntToday + 1 + codeAttempts):D6}";
        }

        var returnOrder = new CustomerReturnOrder
            {
                OrganizationId = orgId,
                WarehouseId = dto.WarehouseId,
                CustomerId = dto.CustomerId,
                OutboundOrderId = dto.OutboundOrderId,
                CustomerFeedbackId = dto.CustomerFeedbackId,
                ReturnCode = returnCode,
                ReturnReason = dto.ReturnReason,
                Note = dto.Note,
                CustomerReturnOrderStatusId = status.Id,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTimeHelper.VietnamNow()
            };

        foreach (var itemDto in dto.Items)
            {
                if (itemDto.QuantityReturned <= 0)
                    return ApiResponse.BadRequest(message: "Số lượng trả của mỗi mặt hàng phải lớn hơn 0.");

                if (itemDto.Allocations == null || itemDto.Allocations.Count == 0)
                    return ApiResponse.BadRequest(message: "Mỗi mặt hàng trả phải được gắn với ít nhất một lô.");

                var productExists = await _context.ProductVariants
                    .AnyAsync(p => p.Id == itemDto.ProductVariantId && p.IsActive && !p.IsDeleted, cancellationToken);
                if (!productExists)
                    return ApiResponse.BadRequest(message: "Sản phẩm trả lại không tồn tại hoặc đã ngừng hoạt động.");

                SalesOrderItem? salesOrderItem = null;
                if (outbound != null)
                {
                    if (!itemDto.OutboundOrderItemId.HasValue)
                        return ApiResponse.BadRequest(message: "Dòng hàng xuất gốc là bắt buộc khi chọn phiếu xuất.");

                    var outboundItem = await _context.OutboundOrderItems
                        .FirstOrDefaultAsync(oi => oi.Id == itemDto.OutboundOrderItemId.Value
                            && oi.OutboundOrderId == outbound.Id
                            && !oi.IsDeleted, cancellationToken);
                    if (outboundItem == null || outboundItem.ProductVariantId != itemDto.ProductVariantId)
                        return ApiResponse.BadRequest(message: "Chi tiết mặt hàng xuất không hợp lệ hoặc không thuộc phiếu xuất gốc.");

                    salesOrderItem = await _context.SalesOrderItems
                        .FirstOrDefaultAsync(s => s.SalesOrderId == outbound.SalesOrderId && s.ProductVariantId == itemDto.ProductVariantId && !s.IsDeleted, cancellationToken);
                    if (salesOrderItem == null)
                        return ApiResponse.BadRequest(message: "Mặt hàng trả lại không nằm trong đơn bán gốc.");
                }

                var returnItem = new CustomerReturnOrderItem
                {
                    ProductVariantId = itemDto.ProductVariantId,
                    QuantityReturned = itemDto.QuantityReturned,
                    QuantityGood = 0,
                    QuantityDamaged = 0,
                    QualityStatus = "GOOD", // Default until inspect
                    CreatedBy = GetCurrentUserId(),
                    CreatedDate = DateTimeHelper.VietnamNow()
                };

                foreach (var allocDto in itemDto.Allocations)
                {
                    if (allocDto.QuantityReturned <= 0)
                        return ApiResponse.BadRequest(message: "Số lượng trả theo lô phải lớn hơn 0.");

                    OutboundOrderItemAllocation? outboundAlloc = null;
                    PaddyLot? paddyLot;
                    int? originalLocationId;

                    if (outbound != null)
                    {
                        if (!allocDto.OutboundOrderItemAllocationId.HasValue)
                            return ApiResponse.BadRequest(message: "Phân bổ xuất gốc là bắt buộc khi chọn phiếu xuất.");

                        outboundAlloc = await _context.OutboundOrderItemAllocations
                            .FirstOrDefaultAsync(a => a.Id == allocDto.OutboundOrderItemAllocationId.Value && !a.IsDeleted, cancellationToken);
                        if (outboundAlloc == null || outboundAlloc.OutboundOrderItemId != itemDto.OutboundOrderItemId)
                            return ApiResponse.BadRequest(message: "Phân bổ chi tiết xuất không hợp lệ.");

                        if (outboundAlloc.PaddyLotId == null)
                            return ApiResponse.BadRequest(message: "Lỗi dữ liệu: Phân bổ chi tiết xuất kho không được gắn với lô gạo nào.");

                        paddyLot = await _context.PaddyLots
                            .FirstOrDefaultAsync(x => x.Id == outboundAlloc.PaddyLotId.Value && !x.IsDeleted, cancellationToken);
                        originalLocationId = outboundAlloc.LocationId;
                    }
                    else
                    {
                        if (!allocDto.PaddyLotId.HasValue)
                            return ApiResponse.BadRequest(message: "Lô hàng là bắt buộc đối với phiếu trả không có đơn xuất gốc.");

                        paddyLot = await _context.PaddyLots
                            .FirstOrDefaultAsync(x => x.Id == allocDto.PaddyLotId.Value
                                && x.WarehouseId == dto.WarehouseId
                                && x.ProductVariantId == itemDto.ProductVariantId
                                && !x.IsDeleted, cancellationToken);
                        originalLocationId = allocDto.OriginalLocationId ?? paddyLot?.LocationId;
                    }

                    if (paddyLot == null)
                        return ApiResponse.BadRequest(message: "Lô hàng trả không hợp lệ, không thuộc kho hoặc không khớp sản phẩm.");

                    // Check return limit
                    if (outboundAlloc != null)
                    {
                        var previousReturnedQty = await _context.CustomerReturnOrderItemAllocations
                            .Where(x => x.OutboundOrderItemAllocationId == allocDto.OutboundOrderItemAllocationId
                                        && x.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed
                                        && !x.IsDeleted
                                        && !x.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted)
                            .SumAsync(x => x.QuantityReturned, cancellationToken);

                        var maxReturnable = outboundAlloc.QuantityPicked - previousReturnedQty;
                        if (allocDto.QuantityReturned > maxReturnable)
                            return ApiResponse.BadRequest(message: $"Số lượng trả lại ({allocDto.QuantityReturned:N3} kg) vượt quá số lượng tối đa có thể trả của phân bổ này ({maxReturnable:N3} kg).");
                    }

                    decimal netUnitSalePrice = salesOrderItem != null && salesOrderItem.QuantityOrdered > 0 
                        ? (salesOrderItem.LineAmount / salesOrderItem.QuantityOrdered) 
                        : salesOrderItem?.UnitSalePrice ?? 0;

                    var returnAlloc = new CustomerReturnOrderItemAllocation
                    {
                        OutboundOrderItemAllocationId = allocDto.OutboundOrderItemAllocationId,
                        PaddyLotId = paddyLot.Id,
                        ProductVariantId = itemDto.ProductVariantId,
                        OriginalLocationId = originalLocationId,
                        QuantityReturned = allocDto.QuantityReturned,
                        QuantityReceived = 0,
                        Disposition = CustomerReturnDisposition.PendingInspection,
                        UnitCreditPrice = netUnitSalePrice,
                        CreatedBy = GetCurrentUserId(),
                        CreatedDate = DateTimeHelper.VietnamNow()
                    };

                    returnItem.Allocations.Add(returnAlloc);
                }

                if (returnItem.Allocations.Sum(a => a.QuantityReturned) != itemDto.QuantityReturned)
                    return ApiResponse.BadRequest(message: "Tổng chi tiết phân bổ trả hàng không khớp với số lượng trả dòng hàng.");

                returnOrder.Items.Add(returnItem);
            }

        await _context.CustomerReturnOrders.AddAsync(returnOrder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Created(data: returnCode, message: "Tạo đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> UpdateAsync(UpdateCustomerReturnOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("UPDATE", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items)
                .ThenInclude(i => i.Allocations)
            .FirstOrDefaultAsync(o => o.Id == dto.Id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Draft)
            return ApiResponse.UnprocessableEntity(message: "Chỉ được cập nhật đơn hàng ở trạng thái Nháp (DRAFT).");

        if (dto.Items == null || dto.Items.Count == 0)
            return ApiResponse.BadRequest(message: "Phiếu trả hàng phải có ít nhất một mặt hàng.");

        var allocationKeys = dto.Items
            .SelectMany(x => x.Allocations ?? new List<CreateCustomerReturnOrderItemAllocationDto>())
            .Select(x => x.OutboundOrderItemAllocationId.HasValue
                ? $"OUT-{x.OutboundOrderItemAllocationId.Value}"
                : x.PaddyLotId.HasValue ? $"LOT-{x.PaddyLotId.Value}" : "INVALID")
            .ToList();
        if (allocationKeys.Contains("INVALID") || allocationKeys.Distinct().Count() != allocationKeys.Count)
            return ApiResponse.BadRequest(message: "Mỗi phân bổ xuất hoặc lô hàng chỉ được khai báo một lần trong phiếu trả.");

        order.ReturnReason = dto.ReturnReason;
        order.Note = dto.Note;
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        // Clear existing items and allocations
        foreach (var oldItem in order.Items)
        {
            foreach (var oldAlloc in oldItem.Allocations)
            {
                oldAlloc.IsDeleted = true;
                oldAlloc.UpdatedBy = GetCurrentUserId();
                oldAlloc.LastModifiedDate = DateTimeHelper.VietnamNow();
            }
            oldItem.IsDeleted = true;
            oldItem.UpdatedBy = GetCurrentUserId();
            oldItem.LastModifiedDate = DateTimeHelper.VietnamNow();
        }

        var outbound = await _context.OutboundOrders
            .Include(o => o.SalesOrder)
            .FirstOrDefaultAsync(o => o.Id == order.OutboundOrderId && !o.IsDeleted, cancellationToken);

        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityReturned <= 0)
                return ApiResponse.BadRequest(message: "Số lượng trả của mỗi mặt hàng phải lớn hơn 0.");

            if (itemDto.Allocations == null || itemDto.Allocations.Count == 0)
                return ApiResponse.BadRequest(message: "Mỗi mặt hàng trả phải được gắn với ít nhất một lô.");

            SalesOrderItem? salesOrderItem = null;
            if (outbound != null)
            {
                if (!itemDto.OutboundOrderItemId.HasValue)
                    return ApiResponse.BadRequest(message: "Dòng hàng xuất gốc là bắt buộc khi phiếu trả có nguồn xuất.");

                var outboundItem = await _context.OutboundOrderItems
                    .FirstOrDefaultAsync(oi => oi.Id == itemDto.OutboundOrderItemId.Value
                        && oi.OutboundOrderId == outbound.Id
                        && !oi.IsDeleted, cancellationToken);
                if (outboundItem == null || outboundItem.ProductVariantId != itemDto.ProductVariantId)
                    return ApiResponse.BadRequest(message: "Chi tiết mặt hàng xuất không hợp lệ hoặc không thuộc phiếu xuất gốc.");

                salesOrderItem = await _context.SalesOrderItems
                    .FirstOrDefaultAsync(s => s.SalesOrderId == outbound.SalesOrderId && s.ProductVariantId == itemDto.ProductVariantId && !s.IsDeleted, cancellationToken);
                if (salesOrderItem == null)
                    return ApiResponse.BadRequest(message: "Mặt hàng trả lại không nằm trong đơn bán gốc.");
            }

            var returnItem = new CustomerReturnOrderItem
            {
                ProductVariantId = itemDto.ProductVariantId,
                QuantityReturned = itemDto.QuantityReturned,
                QuantityGood = 0,
                QuantityDamaged = 0,
                QualityStatus = "GOOD",
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTimeHelper.VietnamNow()
            };

            foreach (var allocDto in itemDto.Allocations)
            {
                if (allocDto.QuantityReturned <= 0)
                    return ApiResponse.BadRequest(message: "Số lượng trả theo lô phải lớn hơn 0.");

                OutboundOrderItemAllocation? outboundAlloc = null;
                PaddyLot? paddyLot;
                int? originalLocationId;

                if (outbound != null)
                {
                    if (!allocDto.OutboundOrderItemAllocationId.HasValue)
                        return ApiResponse.BadRequest(message: "Phân bổ xuất gốc là bắt buộc khi phiếu trả có nguồn xuất.");

                    outboundAlloc = await _context.OutboundOrderItemAllocations
                        .FirstOrDefaultAsync(a => a.Id == allocDto.OutboundOrderItemAllocationId.Value && !a.IsDeleted, cancellationToken);
                    if (outboundAlloc == null || outboundAlloc.OutboundOrderItemId != itemDto.OutboundOrderItemId)
                        return ApiResponse.BadRequest(message: "Phân bổ chi tiết xuất không hợp lệ.");

                    if (outboundAlloc.PaddyLotId == null)
                        return ApiResponse.BadRequest(message: "Lỗi dữ liệu: Phân bổ chi tiết xuất kho không được gắn với lô gạo nào.");

                    paddyLot = await _context.PaddyLots
                        .FirstOrDefaultAsync(x => x.Id == outboundAlloc.PaddyLotId.Value && !x.IsDeleted, cancellationToken);
                    originalLocationId = outboundAlloc.LocationId;

                    var previousReturnedQty = await _context.CustomerReturnOrderItemAllocations
                        .Where(x => x.OutboundOrderItemAllocationId == allocDto.OutboundOrderItemAllocationId
                                    && x.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed
                                    && !x.IsDeleted
                                    && !x.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted
                                    && x.CustomerReturnOrderItem.CustomerReturnOrderId != order.Id)
                        .SumAsync(x => x.QuantityReturned, cancellationToken);

                    var maxReturnable = outboundAlloc.QuantityPicked - previousReturnedQty;
                    if (allocDto.QuantityReturned > maxReturnable)
                        return ApiResponse.BadRequest(message: $"Số lượng trả lại ({allocDto.QuantityReturned:N3} kg) vượt quá số lượng tối đa có thể trả ({maxReturnable:N3} kg).");
                }
                else
                {
                    if (!allocDto.PaddyLotId.HasValue)
                        return ApiResponse.BadRequest(message: "Lô hàng là bắt buộc đối với phiếu trả không có đơn xuất gốc.");

                    paddyLot = await _context.PaddyLots
                        .FirstOrDefaultAsync(x => x.Id == allocDto.PaddyLotId.Value
                            && x.WarehouseId == order.WarehouseId
                            && x.ProductVariantId == itemDto.ProductVariantId
                            && !x.IsDeleted, cancellationToken);
                    originalLocationId = allocDto.OriginalLocationId ?? paddyLot?.LocationId;
                }

                if (paddyLot == null)
                    return ApiResponse.BadRequest(message: "Lô hàng trả không hợp lệ, không thuộc kho hoặc không khớp sản phẩm.");

                decimal netUnitSalePrice = salesOrderItem != null && salesOrderItem.QuantityOrdered > 0 
                    ? (salesOrderItem.LineAmount / salesOrderItem.QuantityOrdered) 
                    : salesOrderItem?.UnitSalePrice ?? 0;

                var returnAlloc = new CustomerReturnOrderItemAllocation
                {
                    OutboundOrderItemAllocationId = allocDto.OutboundOrderItemAllocationId,
                    PaddyLotId = paddyLot.Id,
                    ProductVariantId = itemDto.ProductVariantId,
                    OriginalLocationId = originalLocationId,
                    QuantityReturned = allocDto.QuantityReturned,
                    QuantityReceived = 0,
                    Disposition = CustomerReturnDisposition.PendingInspection,
                    UnitCreditPrice = netUnitSalePrice,
                    CreatedBy = GetCurrentUserId(),
                    CreatedDate = DateTimeHelper.VietnamNow()
                };

                returnItem.Allocations.Add(returnAlloc);
            }

            if (returnItem.Allocations.Sum(a => a.QuantityReturned) != itemDto.QuantityReturned)
                return ApiResponse.BadRequest(message: "Tổng chi tiết phân bổ trả hàng không khớp với số lượng trả dòng hàng.");

            order.Items.Add(returnItem);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Cập nhật đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.Warehouse)
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.OutboundOrder)
                .ThenInclude(o => o.SalesOrder)
            .Include(o => o.Customer)
            .Include(o => o.ApprovedByUser)
            .Include(o => o.ConfirmedByUser)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ProductVariant)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.OriginalLocation)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.RestockLocation)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.QuarantineLocation)
            // Chỉ đọc để map DTO: bỏ change-tracking + tách SELECT theo từng collection
            // (Items × Allocations × 4 nhánh Location) tránh nổ tích Descartes. Kết quả không đổi.
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        var dto = new CustomerReturnOrderDetailDto
        {
            Id = order.Id,
            ReturnCode = order.ReturnCode,
            ReturnReason = order.ReturnReason,
            Note = order.Note,
            StatusId = order.CustomerReturnOrderStatusId,
            StatusCode = order.CustomerReturnOrderStatus.Code,
            StatusName = order.CustomerReturnOrderStatus.Name,
            OutboundOrderId = order.OutboundOrderId,
            OutboundOrderCode = order.OutboundOrderId.HasValue ? $"OB-{order.OutboundOrderId.Value:D5}" : null,
            SalesOrderCode = order.OutboundOrder?.SalesOrder?.SOCode,
            CustomerFeedbackId = order.CustomerFeedbackId,
            CustomerId = order.CustomerId,
            CustomerCode = order.Customer?.Code,
            CustomerName = order.Customer?.Name,
            WarehouseId = order.WarehouseId,
            WarehouseName = order.Warehouse.Name,
            ApprovedCreditAmount = order.ApprovedCreditAmount,
            DebtReductionAmount = order.DebtReductionAmount,
            RefundPendingAmount = order.RefundPendingAmount,
            ApprovedDate = order.ApprovedDate,
            ApprovedByName = order.ApprovedByUser?.LastName + " " + order.ApprovedByUser?.FirstName,
            ConfirmedAt = order.ConfirmedAt,
            ConfirmedByName = order.ConfirmedByUser?.LastName + " " + order.ConfirmedByUser?.FirstName,
            CompletedDate = order.CompletedDate,
            CreatedDate = order.CreatedDate,
            ItemCount = order.Items.Count,
            TotalQuantityReturned = order.Items.Sum(i => i.QuantityReturned),
            TotalQuantityGood = order.Items.Sum(i => i.QuantityGood),
            TotalQuantityDamaged = order.Items.Sum(i => i.QuantityDamaged),
            TotalQuantityRejected = order.Items.SelectMany(i => i.Allocations).Sum(a => a.QuantityRejected),
            PrimaryProductVariantName = order.Items.OrderBy(i => i.Id).Select(i => i.ProductVariant != null ? i.ProductVariant.Name : null).FirstOrDefault(),
            PrimarySKU = order.Items.OrderBy(i => i.Id).Select(i => i.ProductVariant != null ? i.ProductVariant.SKU : null).FirstOrDefault(),
            PrimaryLotCode = order.Items.SelectMany(i => i.Allocations).OrderBy(a => a.Id).Select(a => a.PaddyLot.LotCode).FirstOrDefault()
        };

        foreach (var item in order.Items)
        {
            var itemDto = new CustomerReturnOrderItemDetailDto
            {
                Id = item.Id,
                ProductVariantId = item.ProductVariantId,
                ProductVariantName = item.ProductVariant?.Name,
                SKU = item.ProductVariant?.SKU,
                StandardBagWeightKg = item.ProductVariant?.Weight ?? 0,
                QuantityReturned = item.QuantityReturned,
                QuantityGood = item.QuantityGood,
                QuantityDamaged = item.QuantityDamaged,
                QualityStatus = item.QualityStatus,
                DamageReason = item.DamageReason,
                Note = item.Note
            };

            foreach (var alloc in item.Allocations)
            {
                itemDto.Allocations.Add(new CustomerReturnOrderItemAllocationDetailDto
                {
                    Id = alloc.Id,
                    OutboundOrderItemAllocationId = alloc.OutboundOrderItemAllocationId,
                    PaddyLotId = alloc.PaddyLotId,
                    PaddyLotCode = alloc.PaddyLot.LotCode,
                    ProductVariantId = alloc.ProductVariantId,
                    SKU = item.ProductVariant?.SKU ?? string.Empty,
                    OriginalLocationId = alloc.OriginalLocationId,
                    OriginalLocationCode = alloc.OriginalLocation?.SlotCode ?? (alloc.OriginalLocation != null ? $"LOC-{alloc.OriginalLocationId}" : null),
                    QuantityReturned = alloc.QuantityReturned,
                    QuantityReceived = alloc.QuantityReceived,
                    QuantityGood = alloc.QuantityGood,
                    QuantityDamaged = alloc.QuantityDamaged,
                    QuantityRejected = alloc.QuantityRejected,
                    CreditQuantity = alloc.CreditQuantity,
                    RestockLocationId = alloc.RestockLocationId,
                    RestockLocationCode = alloc.RestockLocation?.SlotCode ?? (alloc.RestockLocation != null ? $"LOC-{alloc.RestockLocationId}" : null),
                    QuarantineLocationId = alloc.QuarantineLocationId,
                    QuarantineLocationCode = alloc.QuarantineLocation?.SlotCode ?? (alloc.QuarantineLocation != null ? $"LOC-{alloc.QuarantineLocationId}" : null),
                    UnitCreditPrice = alloc.UnitCreditPrice,
                    CreditAmount = alloc.CreditAmount,
                    Disposition = alloc.Disposition,
                    RejectedLocationId = alloc.RejectedLocationId,
                    RejectionReason = alloc.RejectionReason,
                    Note = alloc.Note,
                    Bags = string.IsNullOrWhiteSpace(alloc.BagDetailsJson)
                        ? new List<CustomerReturnBagDto>()
                        : JsonSerializer.Deserialize<List<CustomerReturnBagDto>>(alloc.BagDetailsJson) ?? new List<CustomerReturnBagDto>()
                });
            }

            dto.Items.Add(itemDto);
        }

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetReturnSourcesAsync(CustomerReturnSourceQuery query, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync(LookupCodes.Action.Create, cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền tạo phiếu trả hàng.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var keyword = query.Keyword?.Trim();

        var sourceQuery = _context.OutboundOrders
            .AsNoTracking()
            .Where(o => !o.IsDeleted
                && (o.OrganizationId == orgId || (o.OrganizationId == null && o.SalesOrder.OrganizationId == orgId))
                && o.OutboundOrderStatus.Code == OutboundOrderStatusNames.Completed
                && !o.OutboundOrderStatus.IsDeleted
                && !o.SalesOrder.IsDeleted
                && !o.SalesOrder.Customer.IsDeleted
                && o.SalesOrder.Customer.IsActive
                && (o.SalesOrder.Customer.OrganizationId == orgId || o.SalesOrder.Customer.OrganizationId == null)
                && !o.Warehouse.IsDeleted
                && o.Warehouse.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sourceQuery = sourceQuery.Where(o =>
                o.SalesOrder.SOCode.Contains(keyword)
                || o.SalesOrder.Customer.Code.Contains(keyword)
                || o.SalesOrder.Customer.Name.Contains(keyword));
        }

        var candidates = sourceQuery
            .OrderByDescending(o => o.CompletedDate ?? o.CreatedDate)
            .Select(o => new
            {
                OutboundOrderId = o.Id,
                o.SalesOrderId,
                SalesOrderCode = o.SalesOrder.SOCode,
                CustomerId = o.SalesOrder.CustomerId,
                CustomerCode = o.SalesOrder.Customer.Code,
                CustomerName = o.SalesOrder.Customer.Name,
                o.WarehouseId,
                WarehouseCode = o.Warehouse.Code,
                WarehouseName = o.Warehouse.Name,
                DeliveredAt = o.CompletedDate,
                ReturnableQuantity = o.OutboundOrderItems
                    .Where(i => !i.IsDeleted)
                    .SelectMany(i => i.Allocations.Where(a => !a.IsDeleted && a.PaddyLotId.HasValue))
                    .Sum(a => a.QuantityPicked - (_context.CustomerReturnOrderItemAllocations
                        .Where(r => !r.IsDeleted
                            && r.OutboundOrderItemAllocationId == a.Id
                            && !r.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted
                            && r.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed)
                        .Select(r => (decimal?)r.QuantityReturned)
                        .Sum() ?? 0))
            })
            .Where(x => x.ReturnableQuantity > 0);

        var total = await candidates.CountAsync(cancellationToken);
        var rows = await candidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(x => new CustomerReturnSourceOrderDto
            {
                OutboundOrderId = x.OutboundOrderId,
                OutboundOrderCode = $"OB-{x.OutboundOrderId:D5}",
                SalesOrderId = x.SalesOrderId,
                SalesOrderCode = x.SalesOrderCode,
                CustomerId = x.CustomerId,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName,
                WarehouseId = x.WarehouseId,
                WarehouseCode = x.WarehouseCode,
                WarehouseName = x.WarehouseName,
                DeliveredAt = x.DeliveredAt,
                ReturnableQuantity = x.ReturnableQuantity
            })
            .ToList();

        return ApiResponse.Success(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    public async Task<ApiResponse> GetReturnSourceByIdAsync(int outboundOrderId, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync(LookupCodes.Action.Create, cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền tạo phiếu trả hàng.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.OutboundOrders
            .AsNoTracking()
            .Include(o => o.OutboundOrderStatus)
            .Include(o => o.Warehouse)
            .Include(o => o.SalesOrder).ThenInclude(s => s.Customer)
            .Include(o => o.OutboundOrderItems).ThenInclude(i => i.ProductVariant)
            .Include(o => o.OutboundOrderItems).ThenInclude(i => i.Allocations).ThenInclude(a => a.PaddyLot)
            .Include(o => o.OutboundOrderItems).ThenInclude(i => i.Allocations).ThenInclude(a => a.Location)
            .FirstOrDefaultAsync(o => o.Id == outboundOrderId
                && !o.IsDeleted
                && (o.OrganizationId == orgId || (o.OrganizationId == null && o.SalesOrder.OrganizationId == orgId)), cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy phiếu xuất gốc trong tổ chức hiện tại.");
        if (order.OutboundOrderStatus.Code != OutboundOrderStatusNames.Completed)
            return ApiResponse.UnprocessableEntity(message: "Chỉ phiếu xuất đã giao thành công mới có thể tạo trả hàng.");

        var allocationIds = order.OutboundOrderItems
            .Where(i => !i.IsDeleted)
            .SelectMany(i => i.Allocations.Where(a => !a.IsDeleted))
            .Select(a => a.Id)
            .ToList();
        var returnedByAllocation = await _context.CustomerReturnOrderItemAllocations
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.OutboundOrderItemAllocationId.HasValue
                && allocationIds.Contains(r.OutboundOrderItemAllocationId.Value)
                && !r.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted
                && r.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed)
            .GroupBy(r => r.OutboundOrderItemAllocationId!.Value)
            .Select(g => new { AllocationId = g.Key, Quantity = g.Sum(x => x.QuantityReturned) })
            .ToDictionaryAsync(x => x.AllocationId, x => x.Quantity, cancellationToken);

        var itemDtos = order.OutboundOrderItems
            .Where(i => !i.IsDeleted)
            .Select(i => new CustomerReturnSourceItemDto
            {
                OutboundOrderItemId = i.Id,
                ProductVariantId = i.ProductVariantId,
                ProductVariantName = i.ProductVariant.Name,
                SKU = i.ProductVariant.SKU,
                QuantityDelivered = i.QuantityPicked,
                Allocations = i.Allocations
                    .Where(a => !a.IsDeleted && a.PaddyLotId.HasValue)
                    .Select(a =>
                    {
                        var alreadyReturned = returnedByAllocation.GetValueOrDefault(a.Id);
                        return new CustomerReturnSourceAllocationDto
                        {
                            OutboundOrderItemAllocationId = a.Id,
                            PaddyLotId = a.PaddyLotId!.Value,
                            PaddyLotCode = a.PaddyLot?.LotCode ?? string.Empty,
                            OriginalLocationId = a.LocationId,
                            OriginalLocationCode = FormatReturnLocation(a.Location),
                            QuantityDelivered = a.QuantityPicked,
                            QuantityAlreadyReturned = alreadyReturned,
                            QuantityReturnable = Math.Max(0, a.QuantityPicked - alreadyReturned)
                        };
                    })
                    .Where(a => a.QuantityReturnable > 0)
                    .ToList()
            })
            .Select(i =>
            {
                i.QuantityReturnable = i.Allocations.Sum(a => a.QuantityReturnable);
                return i;
            })
            .Where(i => i.QuantityReturnable > 0)
            .ToList();

        if (itemDtos.Count == 0)
            return ApiResponse.Conflict(message: "Toàn bộ hàng của phiếu xuất này đã được trả, không còn số lượng có thể tạo phiếu.");

        return ApiResponse.Success(new CustomerReturnSourceOrderDetailDto
        {
            OutboundOrderId = order.Id,
            OutboundOrderCode = $"OB-{order.Id:D5}",
            SalesOrderId = order.SalesOrderId,
            SalesOrderCode = order.SalesOrder.SOCode,
            CustomerId = order.SalesOrder.CustomerId,
            CustomerCode = order.SalesOrder.Customer.Code,
            CustomerName = order.SalesOrder.Customer.Name,
            WarehouseId = order.WarehouseId,
            WarehouseCode = order.Warehouse.Code,
            WarehouseName = order.Warehouse.Name,
            DeliveredAt = order.CompletedDate,
            ReturnableQuantity = itemDtos.Sum(i => i.QuantityReturnable),
            Items = itemDtos
        });
    }

    private static string FormatReturnLocation(Location? location)
    {
        if (location == null) return string.Empty;
        return string.Join("-", new[] { location.ZoneName, location.ShelfRow, location.ShelfLevel, location.SlotCode }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public async Task<ApiResponse> GetPagedAsync(CustomerReturnOrderPagedQuery query, CancellationToken cancellationToken = default)
    {
        var orgId = await GetCurrentOrganizationIdAsync();
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 1000);

        var dataQuery = _context.CustomerReturnOrders
            .AsNoTracking()
            .Include(o => o.Warehouse)
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Customer)
            .Include(o => o.OutboundOrder)
                .ThenInclude(o => o.SalesOrder)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ProductVariant)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .Where(o => o.OrganizationId == orgId && !o.IsDeleted);

        var total = await dataQuery.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dataQuery = dataQuery.Where(o => o.ReturnCode.Contains(keyword)
                                             || (o.ReturnReason != null && o.ReturnReason.Contains(keyword))
                                             || (o.Customer != null && (o.Customer.Name.Contains(keyword) || o.Customer.Code.Contains(keyword)))
                                             || (o.OutboundOrder != null && o.OutboundOrder.SalesOrder.SOCode.Contains(keyword)));
        }

        if (query.StatusId.HasValue)
            dataQuery = dataQuery.Where(o => o.CustomerReturnOrderStatusId == query.StatusId.Value);

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var statusCode = query.StatusCode.Trim().ToUpper();
            dataQuery = dataQuery.Where(o => o.CustomerReturnOrderStatus.Code == statusCode);
        }

        if (query.CustomerId.HasValue)
            dataQuery = dataQuery.Where(o => o.CustomerId == query.CustomerId.Value);

        if (query.WarehouseId.HasValue)
            dataQuery = dataQuery.Where(o => o.WarehouseId == query.WarehouseId.Value);

        if (query.DateFrom.HasValue)
            dataQuery = dataQuery.Where(o => o.CreatedDate >= query.DateFrom.Value.Date);

        if (query.DateTo.HasValue)
        {
            var dateToExclusive = query.DateTo.Value.Date.AddDays(1);
            dataQuery = dataQuery.Where(o => o.CreatedDate < dateToExclusive);
        }

        var totalFiltered = await dataQuery.CountAsync(cancellationToken);
        var items = await dataQuery
            .OrderByDescending(o => o.CreatedDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            // Tách SELECT theo từng collection (Items, Allocations) để không nhân bản dòng
            // trên cả trang dữ liệu. Kết quả trả về không đổi.
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var list = items.Select(order => new CustomerReturnOrderListDto
        {
            Id = order.Id,
            ReturnCode = order.ReturnCode,
            ReturnReason = order.ReturnReason,
            Note = order.Note,
            StatusId = order.CustomerReturnOrderStatusId,
            StatusCode = order.CustomerReturnOrderStatus.Code,
            StatusName = order.CustomerReturnOrderStatus.Name,
            OutboundOrderId = order.OutboundOrderId,
            OutboundOrderCode = order.OutboundOrderId.HasValue ? $"OB-{order.OutboundOrderId.Value:D5}" : null,
            SalesOrderCode = order.OutboundOrder?.SalesOrder?.SOCode,
            CustomerFeedbackId = order.CustomerFeedbackId,
            CustomerId = order.CustomerId,
            CustomerCode = order.Customer?.Code,
            CustomerName = order.Customer?.Name,
            WarehouseId = order.WarehouseId,
            WarehouseName = order.Warehouse.Name,
            ApprovedCreditAmount = order.ApprovedCreditAmount,
            DebtReductionAmount = order.DebtReductionAmount,
            RefundPendingAmount = order.RefundPendingAmount,
            ApprovedDate = order.ApprovedDate,
            ConfirmedAt = order.ConfirmedAt,
            CompletedDate = order.CompletedDate,
            CreatedDate = order.CreatedDate,
            ItemCount = order.Items.Count,
            TotalQuantityReturned = order.Items.Sum(i => i.QuantityReturned),
            TotalQuantityGood = order.Items.Sum(i => i.QuantityGood),
            TotalQuantityDamaged = order.Items.Sum(i => i.QuantityDamaged),
            TotalQuantityRejected = order.Items.SelectMany(i => i.Allocations).Sum(a => a.QuantityRejected),
            PrimaryProductVariantName = order.Items.OrderBy(i => i.Id).Select(i => i.ProductVariant != null ? i.ProductVariant.Name : null).FirstOrDefault(),
            PrimarySKU = order.Items.OrderBy(i => i.Id).Select(i => i.ProductVariant != null ? i.ProductVariant.SKU : null).FirstOrDefault(),
            PrimaryLotCode = order.Items.SelectMany(i => i.Allocations).OrderBy(a => a.Id).Select(a => a.PaddyLot.LotCode).FirstOrDefault()
        }).ToList();

        var pagedData = new PagingData<CustomerReturnOrderListDto>
        {
            CurrentPage = query.Page,
            PageSize = query.PageSize,
            DataSource = list,
            Total = total,
            TotalFiltered = totalFiltered
        };
        return ApiResponse.Success(pagedData);
    }

    public async Task<ApiResponse> SubmitAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("SUBMIT", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền gửi duyệt đơn trả hàng.");
        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(x => x.CustomerReturnOrderStatus)
            .Include(x => x.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId && !x.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");
        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Draft)
            return ApiResponse.UnprocessableEntity(message: "Chỉ đơn nháp mới được gửi duyệt.");
        if (!order.Items.Any() || order.Items.Any(i => !i.Allocations.Any()))
            return ApiResponse.BadRequest(message: "Đơn trả hàng chưa có đầy đủ dòng hàng và nguồn xuất.");
        var status = await _context.CustomerReturnOrderStatuses.FirstOrDefaultAsync(
            x => x.Code == CustomerReturnOrderStatusNames.PendingApproval && !x.IsDeleted, cancellationToken);
        if (status == null) return ApiResponse.Error(message: "Thiếu trạng thái PENDING_APPROVAL.");
        var now = DateTimeHelper.VietnamNow();
        order.CustomerReturnOrderStatusId = status.Id;
        order.SubmittedAt = now;
        order.SubmittedByUserId = GetCurrentUserId();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = now;
        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Đã gửi duyệt đơn trả hàng.");
    }

    public async Task<ApiResponse> RejectAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("REJECT", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền từ chối đơn trả hàng.");
        if (string.IsNullOrWhiteSpace(reason)) return ApiResponse.BadRequest(message: "Lý do từ chối là bắt buộc.");
        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders.Include(x => x.CustomerReturnOrderStatus)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId && !x.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");
        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.PendingApproval)
            return ApiResponse.UnprocessableEntity(message: "Chỉ đơn đang chờ duyệt mới được từ chối.");
        var status = await _context.CustomerReturnOrderStatuses.FirstOrDefaultAsync(
            x => x.Code == CustomerReturnOrderStatusNames.Rejected && !x.IsDeleted, cancellationToken);
        if (status == null) return ApiResponse.Error(message: "Thiếu trạng thái REJECTED.");
        var now = DateTimeHelper.VietnamNow();
        order.CustomerReturnOrderStatusId = status.Id;
        order.RejectionReason = reason.Trim();
        order.RejectedAt = now;
        order.RejectedByUserId = GetCurrentUserId();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = now;
        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Đã từ chối đơn trả hàng.");
    }

    public async Task<ApiResponse> ReceiveAsync(ReceiveCustomerReturnOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("RECEIVE", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền nhận hàng trả.");
        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(x => x.CustomerReturnOrderStatus)
            .Include(x => x.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == dto.Id && x.OrganizationId == orgId && !x.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");
        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Approved)
            return ApiResponse.UnprocessableEntity(message: "Chỉ đơn đã duyệt mới được nhận hàng.");
        var allocations = order.Items.SelectMany(x => x.Allocations).ToList();
        if (dto.Allocations == null || dto.Allocations.Count != allocations.Count ||
            dto.Allocations.Select(x => x.ReturnAllocationId).Distinct().Count() != allocations.Count)
            return ApiResponse.BadRequest(message: "Phải ghi nhận số lượng thực nhận cho đầy đủ từng lô trả.");
        var now = DateTimeHelper.VietnamNow();
        foreach (var input in dto.Allocations)
        {
            var allocation = allocations.FirstOrDefault(x => x.Id == input.ReturnAllocationId);
            if (allocation == null) return ApiResponse.BadRequest(message: "Có phân bổ không thuộc đơn trả hàng.");
            if (input.QuantityReceived < 0 || input.QuantityReceived > allocation.QuantityReturned)
                return ApiResponse.BadRequest(message: "Số lượng thực nhận phải từ 0 đến số lượng được duyệt trả.");
            allocation.QuantityReceived = input.QuantityReceived;
            allocation.Note = string.IsNullOrWhiteSpace(input.Note) ? allocation.Note : input.Note.Trim();
            allocation.UpdatedBy = GetCurrentUserId();
            allocation.LastModifiedDate = now;
        }
        var status = await _context.CustomerReturnOrderStatuses.FirstOrDefaultAsync(
            x => x.Code == CustomerReturnOrderStatusNames.Received && !x.IsDeleted, cancellationToken);
        if (status == null) return ApiResponse.Error(message: "Thiếu trạng thái RECEIVED.");
        order.CustomerReturnOrderStatusId = status.Id;
        order.ReceivedAt = now;
        order.ReceivedByUserId = GetCurrentUserId();
        order.Note = string.IsNullOrWhiteSpace(dto.Note) ? order.Note : dto.Note.Trim();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = now;
        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Đã ghi nhận hàng trả thực nhận.");
    }

    public async Task<ApiResponse> ApproveAsync(int id, string? note, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("APPROVE", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.PendingApproval)
            return ApiResponse.UnprocessableEntity(message: "Chỉ được duyệt đơn trả hàng đang ở trạng thái DRAFT.");

        // Check if limits are exceeded due to other concurrent returns confirmed in the meantime
        foreach (var item in order.Items)
        {
            foreach (var alloc in item.Allocations)
            {
                if (!alloc.OutboundOrderItemAllocationId.HasValue)
                    continue;

                var outboundAlloc = await _context.OutboundOrderItemAllocations
                    .FirstOrDefaultAsync(a => a.Id == alloc.OutboundOrderItemAllocationId.Value && !a.IsDeleted, cancellationToken);
                if (outboundAlloc == null)
                    return ApiResponse.BadRequest(message: $"Không tìm thấy chi tiết xuất kho ID {alloc.OutboundOrderItemAllocationId} để trả hàng.");

                if (outboundAlloc.PaddyLotId == null)
                    return ApiResponse.BadRequest(message: $"Lỗi dữ liệu: Chi tiết xuất kho ID {alloc.OutboundOrderItemAllocationId} không được gắn với lô gạo nào.");

                var previousReturnedQty = await _context.CustomerReturnOrderItemAllocations
                    .Where(x => x.OutboundOrderItemAllocationId == alloc.OutboundOrderItemAllocationId
                                && x.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed
                                && !x.IsDeleted
                                && !x.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted)
                    .SumAsync(x => x.QuantityReturned, cancellationToken);

                var maxReturnable = outboundAlloc.QuantityPicked - previousReturnedQty;
                if (alloc.QuantityReturned > maxReturnable)
                    return ApiResponse.BadRequest(message: $"Số lượng phân bổ trong đơn ({alloc.QuantityReturned:N3} kg) vượt quá số lượng tối đa có thể trả tại thời điểm hiện tại ({maxReturnable:N3} kg).");
            }
        }

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Approved && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái APPROVED.");

        order.CustomerReturnOrderStatusId = status.Id;
        order.ApprovedBy = GetCurrentUserId();
        order.ApprovedDate = DateTimeHelper.VietnamNow();
        if (!string.IsNullOrEmpty(note))
        {
            order.Note = (string.IsNullOrEmpty(order.Note) ? "" : order.Note + " | ") + "Approve Note: " + note;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Duyệt đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> InspectAsync(InspectCustomerReturnOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("INSPECT", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == dto.Id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Received
            && order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Inspected)
            return ApiResponse.UnprocessableEntity(message: "Chỉ được kiểm định đơn hàng đã nhận hoặc cập nhật kết quả khi đang chờ xác nhận tồn.");

        if (dto.Items == null
            || dto.Items.Count != order.Items.Count
            || dto.Items.Select(x => x.CustomerReturnOrderItemId).Distinct().Count() != dto.Items.Count)
        {
            return ApiResponse.BadRequest(message: "Kết quả kiểm định phải bao gồm đầy đủ và duy nhất từng dòng hàng trả.");
        }

        foreach (var itemDto in dto.Items)
        {
            var item = order.Items.FirstOrDefault(i => i.Id == itemDto.CustomerReturnOrderItemId);
            if (item == null)
                return ApiResponse.BadRequest(message: $"Dòng mặt hàng Id = {itemDto.CustomerReturnOrderItemId} không thuộc đơn hàng này.");

            if (itemDto.QualityStatus != "GOOD" && itemDto.QualityStatus != "DAMAGED" && itemDto.QualityStatus != "EXPIRED")
                return ApiResponse.BadRequest(message: "QualityStatus phải là GOOD, DAMAGED hoặc EXPIRED.");

            if (itemDto.Allocations == null
                || itemDto.Allocations.Count != item.Allocations.Count
                || itemDto.Allocations.Select(x => x.ReturnAllocationId).Distinct().Count() != itemDto.Allocations.Count)
            {
                return ApiResponse.BadRequest(message: $"Kết quả kiểm định dòng hàng Id = {item.Id} phải bao gồm đầy đủ từng lô trả.");
            }

            item.QualityStatus = itemDto.QualityStatus;
            item.DamageReason = itemDto.DamageReason;
            item.UpdatedBy = GetCurrentUserId();
            item.LastModifiedDate = DateTimeHelper.VietnamNow();

            decimal totalGood = 0;
            decimal totalDamaged = 0;

            foreach (var allocDto in itemDto.Allocations)
            {
                var alloc = item.Allocations.FirstOrDefault(a => a.Id == allocDto.ReturnAllocationId);
                if (alloc == null)
                    return ApiResponse.BadRequest(message: $"Phân bổ Id = {allocDto.ReturnAllocationId} không thuộc dòng mặt hàng này.");

                if (allocDto.QuantityGood < 0
                    || allocDto.QuantityDamaged < 0
                    || allocDto.QuantityRejected < 0
                    || allocDto.CreditQuantity < 0)
                {
                    return ApiResponse.BadRequest(message: "Số lượng đạt, hỏng, từ chối và hoàn tiền không được âm.");
                }

                var checkSum = allocDto.QuantityGood + allocDto.QuantityDamaged + allocDto.QuantityRejected;
                if (Math.Abs(checkSum - alloc.QuantityReceived) > 0.001m)
                    return ApiResponse.BadRequest(message: $"Tổng số lượng phân loại ({checkSum:N3} kg) của phân bổ Id = {alloc.Id} phải bằng số lượng thực nhận ({alloc.QuantityReceived:N3} kg).");

                if (allocDto.CreditQuantity > (allocDto.QuantityGood + allocDto.QuantityDamaged))
                    return ApiResponse.BadRequest(message: $"Số lượng hoàn tiền ({allocDto.CreditQuantity:N3} kg) không được vượt quá tổng số lượng nhận lại (Good + Damaged = {allocDto.QuantityGood + allocDto.QuantityDamaged:N3} kg).");

                if ((allocDto.QuantityDamaged > 0 || allocDto.QuantityRejected > 0)
                    && string.IsNullOrWhiteSpace(itemDto.DamageReason))
                {
                    return ApiResponse.BadRequest(message: "Phải nhập lý do đối với hàng hỏng hoặc không nhập lại kho.");
                }

                if (allocDto.Bags.Any(x => x.WeightKg <= 0 || (x.Condition != "GOOD" && x.Condition != "DAMAGED")))
                    return ApiResponse.BadRequest(message: "Mỗi bao trả phải có cân lớn hơn 0 và Condition là GOOD hoặc DAMAGED.");
                if (allocDto.Bags.Count > 0)
                {
                    var standardBagWeight = await _context.ProductVariants.AsNoTracking()
                        .Where(x => x.Id == item.ProductVariantId && !x.IsDeleted)
                        .Select(x => x.Weight)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (standardBagWeight > 0 && allocDto.Bags.Any(x => x.WeightKg > standardBagWeight + 0.001m))
                        return ApiResponse.BadRequest(message: $"Mỗi bao của sản phẩm này không được vượt quá trọng lượng chuẩn {standardBagWeight:0.###} kg.");

                    var goodBagWeight = allocDto.Bags.Where(x => x.Condition == "GOOD").Sum(x => x.WeightKg);
                    var damagedBagWeight = allocDto.Bags.Where(x => x.Condition == "DAMAGED").Sum(x => x.WeightKg);
                    if (Math.Abs(goodBagWeight - allocDto.QuantityGood) > 0.001m || Math.Abs(damagedBagWeight - allocDto.QuantityDamaged) > 0.001m)
                        return ApiResponse.BadRequest(message: "Tổng cân bao GOOD/DAMAGED phải khớp kết quả phân loại tương ứng.");
                }

                // Location Validation
                if (allocDto.QuantityGood > 0)
                {
                    if (!allocDto.RestockLocationId.HasValue)
                        return ApiResponse.BadRequest(message: "Phải chỉ định Restock Location cho hàng chất lượng Tốt (Good).");

                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == allocDto.RestockLocationId.Value && !l.IsDeleted && l.IsActive, cancellationToken);
                    if (loc == null || loc.WarehouseId != order.WarehouseId || loc.IsQuarantine ||
                        loc.IsOutboundStaging || loc.OutboundLockOrderId.HasValue)
                        return ApiResponse.BadRequest(message: "Restock Location không hợp lệ hoặc không thuộc kho của đơn hàng hoặc là khu cách ly.");
                }

                if (allocDto.QuantityDamaged > 0)
                {
                    if (!allocDto.QuarantineLocationId.HasValue)
                        return ApiResponse.BadRequest(message: "Phải chỉ định Quarantine Location cho hàng hỏng/lỗi (Damaged).");

                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == allocDto.QuarantineLocationId.Value && !l.IsDeleted && l.IsActive, cancellationToken);
                    if (loc == null || loc.WarehouseId != order.WarehouseId || !loc.IsQuarantine ||
                        loc.IsOutboundStaging || loc.OutboundLockOrderId.HasValue)
                        return ApiResponse.BadRequest(message: "Quarantine Location không hợp lệ hoặc không thuộc khu cách ly (IsQuarantine = true) của kho.");
                }

                if (allocDto.QuantityRejected > 0)
                {
                    if (!allocDto.RejectedLocationId.HasValue || string.IsNullOrWhiteSpace(allocDto.RejectionReason))
                        return ApiResponse.BadRequest(message: "Hàng bị từ chối phải có vị trí giữ hàng và lý do từ chối.");
                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == allocDto.RejectedLocationId.Value && !l.IsDeleted && l.IsActive, cancellationToken);
                    if (loc == null || loc.WarehouseId != order.WarehouseId || !loc.IsQuarantine || loc.IsOutboundStaging || loc.OutboundLockOrderId.HasValue)
                        return ApiResponse.BadRequest(message: "Vị trí giữ hàng bị từ chối phải là vị trí cách ly hợp lệ của kho.");
                }

                alloc.QuantityGood = allocDto.QuantityGood;
                alloc.QuantityDamaged = allocDto.QuantityDamaged;
                alloc.QuantityRejected = allocDto.QuantityRejected;
                alloc.CreditQuantity = allocDto.CreditQuantity;
                alloc.RestockLocationId = allocDto.RestockLocationId;
                alloc.QuarantineLocationId = allocDto.QuarantineLocationId;
                alloc.RejectedLocationId = allocDto.RejectedLocationId;
                alloc.RejectionReason = allocDto.RejectionReason?.Trim();
                alloc.Disposition = allocDto.QuantityRejected == alloc.QuantityReceived
                    ? CustomerReturnDisposition.ReturnToCustomer
                    : allocDto.QuantityGood == alloc.QuantityReceived
                        ? CustomerReturnDisposition.Restock
                        : allocDto.QuantityDamaged == alloc.QuantityReceived
                            ? CustomerReturnDisposition.Quarantine
                            : CustomerReturnDisposition.Mixed;
                alloc.CreditAmount = allocDto.CreditQuantity * alloc.UnitCreditPrice;
                alloc.Note = allocDto.Note;
                alloc.BagDetailsJson = allocDto.Bags.Count == 0 ? null : JsonSerializer.Serialize(allocDto.Bags);
                alloc.UpdatedBy = GetCurrentUserId();
                alloc.LastModifiedDate = DateTimeHelper.VietnamNow();

                totalGood += allocDto.QuantityGood;
                totalDamaged += allocDto.QuantityDamaged;
            }

            item.QuantityGood = totalGood;
            item.QuantityDamaged = totalDamaged;
            var totalReceived = item.Allocations.Sum(a => a.QuantityReceived);
            item.QualityStatus = totalGood == totalReceived ? "GOOD"
                : totalDamaged == totalReceived ? "DAMAGED" : "MIXED";
        }

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Inspected && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái INSPECTED.");

        order.CustomerReturnOrderStatusId = status.Id;
        order.InspectedAt = DateTimeHelper.VietnamNow();
        order.InspectedByUserId = GetCurrentUserId();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        // Save financial amount summary to return order
        var totalCredit = order.Items.SelectMany(i => i.Allocations).Sum(a => a.CreditAmount);
        order.ApprovedCreditAmount = totalCredit;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Kiểm định đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> GetImpactPreviewAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("PREVIEW", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Inspected && order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Confirmed)
            return ApiResponse.UnprocessableEntity(message: "Chỉ xem được tác động đối với đơn hàng đang ở trạng thái INSPECTED hoặc CONFIRMED.");

        var approvedCreditAmount = order.Items.SelectMany(i => i.Allocations).Sum(a => a.CreditAmount);

        var partyDebt = await _context.PartyDebts
            .FirstOrDefaultAsync(d => d.PartyType == LookupCodes.PartyType.Customer && d.PartyId == order.CustomerId && d.Direction == LookupCodes.DebtDirection.Receivable && d.IsActive && !d.IsDeleted, cancellationToken);

        decimal currentReceivableBalance = partyDebt?.CurrentBalance ?? 0;
        decimal debtReductionAmount = Math.Min(currentReceivableBalance, approvedCreditAmount);
        decimal refundPendingAmount = Math.Max(0, approvedCreditAmount - debtReductionAmount);

        var preview = new CustomerReturnImpactPreviewDto
        {
            CustomerReturnOrderId = order.Id,
            ApprovedCreditAmount = approvedCreditAmount,
            CurrentReceivableBalance = currentReceivableBalance,
            DebtReductionAmount = debtReductionAmount,
            RefundPendingAmount = refundPendingAmount
        };

        // Group allocations for inventory impact
        var grouped = order.Items.SelectMany(i => i.Allocations)
            .GroupBy(a => new { a.PaddyLotId, a.PaddyLot.LotCode, a.ProductVariantId, a.RestockLocationId, a.QuarantineLocationId });

        foreach (var group in grouped)
        {
            var first = group.First();
            var sku = await _context.ProductVariants
                .Where(pv => pv.Id == group.Key.ProductVariantId)
                .Select(pv => pv.SKU)
                .FirstOrDefaultAsync(cancellationToken);

            preview.InventoryImpact.Add(new CustomerReturnInventoryImpactDto
            {
                PaddyLotId = group.Key.PaddyLotId,
                PaddyLotCode = group.Key.LotCode,
                ProductVariantId = group.Key.ProductVariantId,
                SKU = sku ?? "",
                GoodQuantityKg = group.Sum(a => a.QuantityGood),
                DamagedQuantityKg = group.Sum(a => a.QuantityDamaged),
                RejectedQuantityKg = group.Sum(a => a.QuantityRejected),
                RestockLocationId = group.Key.RestockLocationId,
                QuarantineLocationId = group.Key.QuarantineLocationId
            });
        }

        return ApiResponse.Success(preview);
    }

    public async Task<ApiResponse> ConfirmAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("CONFIRM", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.OutboundOrderItemAllocation)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        // Idempotency check
        if (order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed)
            return ApiResponse.BadRequest(message: "Đơn trả hàng đã được xác nhận trước đó.", code: "CUSTOMER_RETURN_ALREADY_CONFIRMED");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Inspected)
            return ApiResponse.UnprocessableEntity(message: "Chỉ xác nhận đơn trả hàng đang ở trạng thái INSPECTED.");

        // Check deduplication key
        var deduplicationKey = $"CRT-CONFIRM-{order.Id}";
        var isDuplicate = await _context.DebtTransactions
            .AnyAsync(x => x.DeduplicationKey == deduplicationKey, cancellationToken);
        if (isDuplicate)
            return ApiResponse.Conflict(message: "Yêu cầu xác nhận đơn trả hàng bị trùng lặp.", code: "CUSTOMER_RETURN_ALREADY_CONFIRMED");

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Confirmed && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái CONFIRMED.");

        // Fail fast before making any inventory/debt changes. A pre-existing mismatch must be
        // reconciled by stock-taking instead of being hidden by a customer return.
        if (_bagInvariantService != null)
        {
            var targetLotLocations = order.Items.SelectMany(i => i.Allocations)
                .SelectMany(a => new[]
                {
                    a.QuantityGood > 0 && a.RestockLocationId.HasValue ? (a.PaddyLotId, a.RestockLocationId.Value) : ((int, int)?)null,
                    a.QuantityDamaged > 0 && a.QuarantineLocationId.HasValue ? (a.PaddyLotId, a.QuarantineLocationId.Value) : ((int, int)?)null,
                    a.QuantityRejected > 0 && a.RejectedLocationId.HasValue ? (a.PaddyLotId, a.RejectedLocationId.Value) : ((int, int)?)null
                })
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            try
            {
                foreach (var pair in targetLotLocations)
                    await _bagInvariantService.ValidateLotLocationAsync(pair.Item1, pair.Item2, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot confirm customer return {OrderId}: inventory and bag stock are inconsistent", order.Id);
                return ApiResponse.Conflict(
                    message: $"Không thể xác nhận vì dữ liệu tồn theo bao đang lệch với tồn kho. {ex.Message} Vui lòng kiểm kê/điều chỉnh tồn trước rồi thử lại.",
                    code: "LOT_LOCATION_INVENTORY_MISMATCH");
            }
        }

        // Perform inside database transaction
        using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var userId = GetCurrentUserId();
            var now = DateTimeHelper.VietnamNow();

            // Set bypass for automatic interceptor
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Items["BypassLocationOccupancyInterceptor"] = true;
            }

            var allocations = order.Items.SelectMany(i => i.Allocations).ToList();

            // Re-verify returned quantity limits against original picking limits to prevent race condition
            foreach (var alloc in allocations)
            {
                if (!alloc.OutboundOrderItemAllocationId.HasValue)
                    continue;

                var alreadyReturned = await _context.CustomerReturnOrderItemAllocations
                    .Where(x => x.OutboundOrderItemAllocationId == alloc.OutboundOrderItemAllocationId 
                             && x.CustomerReturnOrderItem.CustomerReturnOrder.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed
                             && !x.IsDeleted
                             && !x.CustomerReturnOrderItem.IsDeleted
                             && !x.CustomerReturnOrderItem.CustomerReturnOrder.IsDeleted)
                    .SumAsync(x => x.QuantityReturned, cancellationToken);

                var maxReturnable = alloc.OutboundOrderItemAllocation!.QuantityPicked - alreadyReturned;
                if (alloc.QuantityReturned > maxReturnable)
                {
                    throw new Exception("RETURN_QUANTITY_EXCEEDED");
                }
                // Chạm dòng phân bổ gốc để RowVersion bảo vệ hai yêu cầu trả đồng thời.
                alloc.OutboundOrderItemAllocation.UpdatedBy = userId;
                alloc.OutboundOrderItemAllocation.LastModifiedDate = now;
            }

            // 1. Process Restock (Good) allocations
            var goodAllocations = allocations.Where(a => a.QuantityGood > 0).ToList();
            foreach (var alloc in goodAllocations)
            {
                var locationId = alloc.RestockLocationId!.Value;

                // Atomic UPDATE location with capacity limit check
                var affected = await _context.ExecuteSqlRawAsync(
                    "UPDATE Location SET CurrentOccupancy = CurrentOccupancy + {0}, CurrentProductVariantId = {1}, LastModifiedDate = {2}, UpdatedBy = {3} " +
                    "WHERE Id = {4} AND WarehouseId = {5} AND IsActive = 1 AND IsDeleted = 0 AND IsOutboundStaging = 0 AND OutboundLockOrderId IS NULL AND (IsSingleTypeColumn = 0 OR CurrentProductVariantId IS NULL OR CurrentProductVariantId = {1}) AND (MaxCapacity IS NULL OR CurrentOccupancy + {0} <= MaxCapacity) AND IsQuarantine = 0",
                    new object[] { alloc.QuantityGood, alloc.ProductVariantId, now, userId, locationId, order.WarehouseId }, cancellationToken);

                if (affected == 0)
                {
                    throw new Exception("RETURN_LOCATION_CHANGED");
                }

                // Update Inventory
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(inv => inv.WarehouseId == order.WarehouseId
                                                && inv.LocationId == locationId
                                                && inv.ProductVariantId == alloc.ProductVariantId
                                                && inv.PaddyLotId == alloc.PaddyLotId
                                                && !inv.IsDeleted, cancellationToken);

                decimal beforeQty = 0;
                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        WarehouseId = order.WarehouseId,
                        LocationId = locationId,
                        ProductVariantId = alloc.ProductVariantId,
                        PaddyLotId = alloc.PaddyLotId,
                        QuantityOnHand = alloc.QuantityGood,
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _context.Inventories.AddAsync(inventory, cancellationToken);
                }
                else
                {
                    beforeQty = inventory.QuantityOnHand;
                    inventory.QuantityOnHand += alloc.QuantityGood;
                    inventory.UpdatedBy = userId;
                    inventory.LastModifiedDate = now;
                }

                // Create Inventory Transaction
                var invTx = new InventoryTransaction
                {
                    Inventory = inventory,
                    WarehouseId = order.WarehouseId,
                    LocationId = locationId,
                    ProductVariantId = alloc.ProductVariantId,
                    PaddyLotId = alloc.PaddyLotId,
                    TransactionType = InventoryTransactionTypeConstants.CustomerReturnRestock,
                    BeforeQuantity = beforeQty,
                    Quantity = alloc.QuantityGood,
                    AfterQuantity = beforeQty + alloc.QuantityGood,
                    ReferenceType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                    ReferenceId = order.Id,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                // Quy đổi Before/After sang TỔNG TỒN CỦA CỘT (cộng tồn các dòng khác cùng vị trí).
                if (invTx.LocationId.HasValue)
                {
                    var otherOnHand = await _context.Inventories
                        .Where(i => i.LocationId == invTx.LocationId.Value && !i.IsDeleted && i.Id != inventory.Id)
                        .SumAsync(i => i.QuantityOnHand, cancellationToken);
                    invTx.BeforeQuantity += otherOnHand;
                    invTx.AfterQuantity += otherOnHand;
                }
                await _context.InventoryTransactions.AddAsync(invTx, cancellationToken);
                await PackReturnedGoodsAsync(alloc, locationId, alloc.QuantityGood, false, order, userId, now, cancellationToken);
            }

            // 2. Process Quarantine (Damaged) allocations
            var nonSellableMovements = allocations.SelectMany(a => new[]
            {
                a.QuantityDamaged > 0 && a.QuarantineLocationId.HasValue
                    ? new { Allocation = a, Quantity = a.QuantityDamaged, LocationId = a.QuarantineLocationId.Value, Type = InventoryTransactionTypeConstants.CustomerReturnQuarantine }
                    : null,
                a.QuantityRejected > 0 && a.RejectedLocationId.HasValue
                    ? new { Allocation = a, Quantity = a.QuantityRejected, LocationId = a.RejectedLocationId.Value, Type = InventoryTransactionTypeConstants.CustomerReturnRejectedHold }
                    : null
            }).Where(x => x != null).ToList();
            foreach (var movement in nonSellableMovements)
            {
                var alloc = movement!.Allocation;
                var movementQuantity = movement.Quantity;
                var locationId = movement.LocationId;
                var movementType = movement.Type;

                // Atomic UPDATE location with capacity limit check
                var affected = await _context.ExecuteSqlRawAsync(
                    "UPDATE Location SET CurrentOccupancy = CurrentOccupancy + {0}, CurrentProductVariantId = {1}, LastModifiedDate = {2}, UpdatedBy = {3} " +
                    "WHERE Id = {4} AND WarehouseId = {5} AND IsActive = 1 AND IsDeleted = 0 AND IsOutboundStaging = 0 AND OutboundLockOrderId IS NULL AND (IsSingleTypeColumn = 0 OR CurrentProductVariantId IS NULL OR CurrentProductVariantId = {1}) AND (MaxCapacity IS NULL OR CurrentOccupancy + {0} <= MaxCapacity) AND IsQuarantine = 1",
                    new object[] { movementQuantity, alloc.ProductVariantId, now, userId, locationId, order.WarehouseId }, cancellationToken);

                if (affected == 0)
                {
                    throw new Exception("RETURN_LOCATION_CHANGED");
                }

                // Update Inventory
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(inv => inv.WarehouseId == order.WarehouseId
                                                && inv.LocationId == locationId
                                                && inv.ProductVariantId == alloc.ProductVariantId
                                                && inv.PaddyLotId == alloc.PaddyLotId
                                                && !inv.IsDeleted, cancellationToken);

                decimal beforeQty = 0;
                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        WarehouseId = order.WarehouseId,
                        LocationId = locationId,
                        ProductVariantId = alloc.ProductVariantId,
                        PaddyLotId = alloc.PaddyLotId,
                        QuantityOnHand = movementQuantity,
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _context.Inventories.AddAsync(inventory, cancellationToken);
                }
                else
                {
                    beforeQty = inventory.QuantityOnHand;
                    inventory.QuantityOnHand += movementQuantity;
                    inventory.UpdatedBy = userId;
                    inventory.LastModifiedDate = now;
                }

                // Create Inventory Transaction
                var invTx = new InventoryTransaction
                {
                    Inventory = inventory,
                    WarehouseId = order.WarehouseId,
                    LocationId = locationId,
                    ProductVariantId = alloc.ProductVariantId,
                    PaddyLotId = alloc.PaddyLotId,
                    TransactionType = movementType,
                    BeforeQuantity = beforeQty,
                    Quantity = movementQuantity,
                    AfterQuantity = beforeQty + movementQuantity,
                    ReferenceType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                    ReferenceId = order.Id,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                // Quy đổi Before/After sang TỔNG TỒN CỦA CỘT (cộng tồn các dòng khác cùng vị trí).
                if (invTx.LocationId.HasValue)
                {
                    var otherOnHand = await _context.Inventories
                        .Where(i => i.LocationId == invTx.LocationId.Value && !i.IsDeleted && i.Id != inventory.Id)
                        .SumAsync(i => i.QuantityOnHand, cancellationToken);
                    invTx.BeforeQuantity += otherOnHand;
                    invTx.AfterQuantity += otherOnHand;
                }
                await _context.InventoryTransactions.AddAsync(invTx, cancellationToken);
                await PackReturnedGoodsAsync(alloc, locationId, movementQuantity, true, order, userId, now, cancellationToken);
            }

            // 3. Update PaddyLots remaining weights
            var lotIds = allocations.Select(a => a.PaddyLotId).Distinct().ToList();
            var storedStatus = await _context.LotStatuses
                .FirstOrDefaultAsync(ls => ls.Code == "IN_STOCK" && !ls.IsDeleted, cancellationToken);

            foreach (var lotId in lotIds)
            {
                var lot = await _context.PaddyLots
                    .Include(pl => pl.Status)
                    .FirstOrDefaultAsync(pl => pl.Id == lotId && !pl.IsDeleted, cancellationToken);

                if (lot != null)
                {
                    var addedWeight = allocations.Where(a => a.PaddyLotId == lotId).Sum(a => a.QuantityGood + a.QuantityDamaged + a.QuantityRejected);
                    var newRemainingWeight = lot.RemainingWeightKg + addedWeight;

                    if (newRemainingWeight > lot.InitialWeightKg)
                    {
                        throw new Exception("LOT_REMAINING_WEIGHT_EXCEEDED");
                    }

                    lot.RemainingWeightKg = newRemainingWeight;
                    lot.UpdatedBy = userId;
                    lot.LastModifiedDate = now;

                    // If previously depleted, change status back to IN_STOCK
                    if (lot.RemainingWeightKg > 0 && lot.Status.Code == "DEPLETED" && storedStatus != null)
                    {
                        lot.StatusId = storedStatus.Id;
                    }
                }
            }

            // 4. Update customer party debt and create debt transaction
            var approvedCredit = allocations.Sum(a => a.CreditAmount);

            var partyDebt = await _context.PartyDebts
                .FirstOrDefaultAsync(d => d.PartyType == LookupCodes.PartyType.Customer && d.PartyId == order.CustomerId && d.Direction == LookupCodes.DebtDirection.Receivable && d.IsActive && !d.IsDeleted, cancellationToken);

            if (partyDebt == null && order.CustomerId.HasValue)
            {
                partyDebt = new PartyDebt
                {
                    OrganizationId = order.OrganizationId,
                    PartyType = LookupCodes.PartyType.Customer,
                    PartyId = order.CustomerId.Value,
                    Direction = LookupCodes.DebtDirection.Receivable,
                    OpeningBalance = 0,
                    CurrentBalance = 0,
                    IsActive = true,
                    CreatedDate = now,
                    CreatedBy = userId
                };
                await _context.PartyDebts.AddAsync(partyDebt, cancellationToken);
            }

            PartyDebt? payableDebt = null;
            decimal debtReduction = 0;
            decimal refundPending = approvedCredit;

            if (partyDebt != null)
            {
                var beforeBalance = partyDebt.CurrentBalance;
                debtReduction = Math.Min(beforeBalance, approvedCredit);
                refundPending = Math.Max(0, approvedCredit - debtReduction);

                if (debtReduction > 0)
                {
                    partyDebt.CurrentBalance -= debtReduction;
                    partyDebt.UpdatedBy = userId;
                    partyDebt.LastModifiedDate = now;

                    var debtTx = new DebtTransaction
                    {
                        PartyDebt = partyDebt,
                        TransactionType = LookupCodes.DebtTransactionType.ReturnCredit,
                        Amount = debtReduction,
                        BalanceAfter = partyDebt.CurrentBalance,
                        RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                        RefId = order.Id,
                        TransactionDate = now,
                        Note = $"Khấu trừ công nợ từ đơn trả hàng {order.ReturnCode}",
                        DeduplicationKey = deduplicationKey,
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _context.DebtTransactions.AddAsync(debtTx, cancellationToken);
                }


                if (refundPending > 0)
                {
                    // #18: Khoản phải hoàn trả cho khách (vượt dư nợ) là công nợ hướng PAYABLE,
                    // KHÔNG đẩy số dư RECEIVABLE xuống âm. Tìm/tạo PartyDebt PAYABLE riêng cho khách.
                    payableDebt = await _context.PartyDebts.FirstOrDefaultAsync(d =>
                        d.PartyType == LookupCodes.PartyType.Customer &&
                        d.PartyId == order.CustomerId &&
                        d.Direction == LookupCodes.DebtDirection.Payable &&
                        d.IsActive && !d.IsDeleted, cancellationToken);

                    if (payableDebt == null && order.CustomerId.HasValue)
                    {
                        payableDebt = new PartyDebt
                        {
                            OrganizationId = order.OrganizationId,
                            PartyType = LookupCodes.PartyType.Customer,
                            PartyId = order.CustomerId.Value,
                            Direction = LookupCodes.DebtDirection.Payable,
                            OpeningBalance = 0,
                            CurrentBalance = 0,
                            IsActive = true,
                            CreatedDate = now,
                            CreatedBy = userId
                        };
                        await _context.PartyDebts.AddAsync(payableDebt, cancellationToken);
                    }

                    if (payableDebt != null)
                    {
                        payableDebt.CurrentBalance += refundPending;
                        payableDebt.UpdatedBy = userId;
                        payableDebt.LastModifiedDate = now;

                        var refundTx = new DebtTransaction
                        {
                            PartyDebt = payableDebt,
                            TransactionType = LookupCodes.DebtTransactionType.RefundPayable,
                            Amount = refundPending,
                            BalanceAfter = payableDebt.CurrentBalance,
                            RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                            RefId = order.Id,
                            TransactionDate = now,
                            Note = $"Ghi nhận khoản phải hoàn trả (Refund Payable) cho khách của đơn trả hàng {order.ReturnCode}",
                            DeduplicationKey = deduplicationKey + "-REFUND",
                            CreatedBy = userId,
                            CreatedDate = now
                        };
                        await _context.DebtTransactions.AddAsync(refundTx, cancellationToken);
                    }
                }
            }

            // 5. Update order state
            order.CustomerReturnOrderStatusId = status.Id;
            order.ConfirmedAt = now;
            order.ConfirmedByUserId = userId;
            order.CompletedDate = now;
            
            order.ApprovedCreditAmount = approvedCredit;
            order.DebtReductionAmount = debtReduction;
            order.RefundPendingAmount = refundPending;
            order.RefundStatus = refundPending > 0
                ? CustomerReturnRefundStatus.Pending
                : CustomerReturnRefundStatus.NotApplicable;

            order.UpdatedBy = userId;
            order.LastModifiedDate = now;

            await _context.SaveChangesAsync(cancellationToken);
            if (_bagInvariantService != null)
            {
                var affectedLotLocations = allocations
                    .SelectMany(a => new[]
                    {
                        a.QuantityGood > 0 && a.RestockLocationId.HasValue ? (a.PaddyLotId, a.RestockLocationId.Value) : ((int, int)?)null,
                        a.QuantityDamaged > 0 && a.QuarantineLocationId.HasValue ? (a.PaddyLotId, a.QuarantineLocationId.Value) : ((int, int)?)null,
                        a.QuantityRejected > 0 && a.RejectedLocationId.HasValue ? (a.PaddyLotId, a.RejectedLocationId.Value) : ((int, int)?)null
                    })
                    .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
                foreach (var pair in affectedLotLocations)
                    await _bagInvariantService.ValidateLotLocationAsync(pair.Item1, pair.Item2, cancellationToken);
            }
            await dbTransaction.CommitAsync(cancellationToken);

            // Enqueue targeted background job evaluation for JOB-04 after transaction completes
            if (_scheduledJobService != null)
            {
                if (partyDebt != null && debtReduction > 0)
                {
                    _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(partyDebt.Id, CancellationToken.None));
                }
                if (payableDebt != null && refundPending > 0)
                {
                    _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(payableDebt.Id, CancellationToken.None));
                }
            }

            return ApiResponse.Success(message: "Xác nhận đơn trả hàng thành công.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Concurrent customer return confirmation for order {OrderId}", order.Id);
            return ApiResponse.Conflict(message: "Số lượng đã trả vừa thay đổi bởi yêu cầu khác. Vui lòng tải lại dữ liệu.", code: "CUSTOMER_RETURN_CONCURRENCY_CONFLICT");
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error occurred during ConfirmAsync of CustomerReturnOrder {OrderId}", order.Id);

            if (ex.Message == "RETURN_LOCATION_CHANGED")
            {
                return ApiResponse.Conflict(message: "Vị trí tiếp nhận đã thay đổi hoặc đầy dung tích chứa.", code: "RETURN_LOCATION_CHANGED");
            }
            if (ex.Message == "LOT_REMAINING_WEIGHT_EXCEEDED")
            {
                return ApiResponse.BadRequest(message: "Trọng lượng lúa nhập lại vượt quá trọng lượng ban đầu của lô.", code: "LOT_REMAINING_WEIGHT_EXCEEDED");
            }
            if (ex.Message == "RETURN_QUANTITY_EXCEEDED")
            {
                return ApiResponse.BadRequest(message: "Số lượng trả hàng vượt quá số lượng đã xuất bán thực tế.", code: "RETURN_QUANTITY_EXCEEDED");
            }

            throw;
        }
        finally
        {
            // Remove bypass header
            _httpContextAccessor.HttpContext?.Items.Remove("BypassLocationOccupancyInterceptor");
        }
    }

    private async Task PackReturnedGoodsAsync(CustomerReturnOrderItemAllocation allocation, int locationId,
        decimal quantity, bool quarantined, CustomerReturnOrder order, int userId, DateTime now, CancellationToken cancellationToken)
    {
        if (quantity <= 0) return;
        // Một số unit-test cũ dùng mock IApplicationDbContext tối giản, chưa cấu hình các DbSet quản lý bao.
        if (_context.PaddyLotBags == null || _context.PaddyLotBagMovements == null || _context.ProductVariants == null) return;

        // ProductVariant.Weight là nguồn dữ liệu duy nhất cho khối lượng bao chuẩn.
        // Không dùng SystemConfig "StandardBagWeightKg:{variantId}" vì khóa chứa ID khó quản trị
        // và có thể lệch với giá trị mà luồng xay xát đang sử dụng.
        var variant = await _context.ProductVariants.AsNoTracking()
            .Where(x => x.Id == allocation.ProductVariantId && !x.IsDeleted)
            .Select(x => new { x.SKU, x.Weight })
            .FirstOrDefaultAsync(cancellationToken);
        var standardWeight = variant?.Weight ?? 0;
        if (standardWeight <= 0)
        {
            var bagTracked = await _context.PaddyLotBags.AsNoTracking()
                .AnyAsync(x => x.LotId == allocation.PaddyLotId && !x.IsDeleted, cancellationToken);
            if (!bagTracked) return; // Giữ tương thích tồn cũ chưa quản lý vật lý theo bao.
            throw new InvalidOperationException(
                $"Biến thể '{variant?.SKU ?? allocation.ProductVariantId.ToString()}' chưa cấu hình khối lượng bao chuẩn để đóng bao hàng trả.");
        }

        var remaining = quantity;
        var nextStack = (await _context.PaddyLotBags
            .Where(x => x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
            .MaxAsync(x => (int?)x.StackOrder, cancellationToken) ?? 0) + 1;
        var nextBagNo = (await _context.PaddyLotBags.Where(x => x.LotId == allocation.PaddyLotId && !x.IsDeleted)
            .MaxAsync(x => (int?)x.BagNo, cancellationToken) ?? 0) + 1;

        if (!quarantined)
        {
            var openKey = $"{allocation.ProductVariantId}:{order.WarehouseId}:{locationId}";
            var open = await _context.PaddyLotBags.Include(x => x.Contents)
                .FirstOrDefaultAsync(x => x.OpenBagKey == openKey && x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, cancellationToken);
            if (open != null && remaining > 0)
            {
                if (open.StackOrder != 0)
                    throw new InvalidOperationException(
                        $"Bao mở #{open.BagNo} đang bị bao khác chặn phía trên nên không thể bổ sung hàng trả.");
                var before = open.WeightKg;
                var topUp = Math.Min(remaining, standardWeight - open.WeightKg);
                if (topUp > 0)
                {
                    open.Contents.Add(new PaddyLotBagContent { LotId = allocation.PaddyLotId, WeightKg = topUp, CreatedBy = userId, CreatedDate = now });
                    open.WeightKg += topUp;
                    open.IsFull = open.WeightKg >= standardWeight - 0.001m;
                    open.OpenBagKey = open.IsFull ? null : openKey;
                    open.UpdatedBy = userId; open.LastModifiedDate = now;
                    await _context.PaddyLotBagMovements.AddAsync(new PaddyLotBagMovement
                    {
                        BagId = open.Id, MovementType = PaddyLotBagMovementTypes.CustomerReturn,
                        FromLocationId = locationId, ToLocationId = locationId, WeightKg = topUp,
                        BeforeWeightKg = before, AfterWeightKg = open.WeightKg,
                        ReferenceType = InventoryReferenceTypeConstants.CustomerReturnOrder, ReferenceId = order.Id,
                        ReferenceItemId = allocation.Id, CreatedBy = userId, CreatedDate = now
                    }, cancellationToken);
                    remaining -= topUp;
                }
            }
        }

        while (remaining > 0.001m)
        {
            var weight = Math.Min(standardWeight, remaining);
            var isFull = weight >= standardWeight - 0.001m;
            var bag = new PaddyLotBag
            {
                LotId = allocation.PaddyLotId, BagNo = nextBagNo++, WeightKg = weight, LocationId = locationId,
                Status = PaddyLotBagStatuses.Stored, QrCode = $"PLB-{Guid.NewGuid():N}".ToUpperInvariant(),
                StackOrder = isFull ? nextStack++ : 0, StandardWeightKg = standardWeight, IsFull = isFull,
                BagKind = quarantined ? PaddyLotBagKinds.Quarantine : PaddyLotBagKinds.Finished,
                OpenBagKey = !quarantined && !isFull ? $"{allocation.ProductVariantId}:{order.WarehouseId}:{locationId}" : null,
                CreatedBy = userId, CreatedDate = now,
                Contents = new List<PaddyLotBagContent>
                {
                    new() { LotId = allocation.PaddyLotId, WeightKg = weight, CreatedBy = userId, CreatedDate = now }
                }
            };
            bag.Movements.Add(new PaddyLotBagMovement
            {
                MovementType = PaddyLotBagMovementTypes.CustomerReturn, ToLocationId = locationId,
                WeightKg = weight, BeforeWeightKg = 0, AfterWeightKg = weight,
                ReferenceType = InventoryReferenceTypeConstants.CustomerReturnOrder, ReferenceId = order.Id,
                ReferenceItemId = allocation.Id, CreatedBy = userId, CreatedDate = now
            });
            await _context.PaddyLotBags.AddAsync(bag, cancellationToken);
            remaining -= weight;
        }
    }

    public async Task<ApiResponse> RegisterRefundAsync(int id, RegisterCustomerReturnRefundDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("REFUND", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền ghi nhận hoàn tiền.");
        if (dto.Amount <= 0 || string.IsNullOrWhiteSpace(dto.PaymentReference))
            return ApiResponse.BadRequest(message: "Số tiền và mã tham chiếu thanh toán là bắt buộc.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders.Include(x => x.CustomerReturnOrderStatus)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId && !x.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");
        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Confirmed)
            return ApiResponse.UnprocessableEntity(message: "Chỉ được hoàn tiền cho đơn trả hàng đã hoàn tất.");
        if (dto.Amount > order.RefundPendingAmount)
            return ApiResponse.BadRequest(message: "Số tiền hoàn vượt quá số tiền còn phải hoàn.");

        var paymentReference = dto.PaymentReference.Trim().ToUpperInvariant();
        var deduplicationKey = $"CRT-REFUND-{order.Id}-{paymentReference}";
        if (await _context.DebtTransactions.AnyAsync(x => x.DeduplicationKey == deduplicationKey, cancellationToken))
            return ApiResponse.Conflict(message: "Giao dịch hoàn tiền đã được ghi nhận trước đó.", code: "CUSTOMER_RETURN_REFUND_DUPLICATE");

        var payable = await _context.PartyDebts.FirstOrDefaultAsync(x =>
            x.OrganizationId == orgId && x.PartyType == LookupCodes.PartyType.Customer &&
            x.PartyId == order.CustomerId && x.Direction == LookupCodes.DebtDirection.Payable &&
            x.IsActive && !x.IsDeleted, cancellationToken);
        if (payable == null || payable.CurrentBalance < dto.Amount)
            return ApiResponse.Conflict(message: "Công nợ phải trả khách không đủ để ghi nhận giao dịch hoàn tiền.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeHelper.VietnamNow();
        payable.CurrentBalance -= dto.Amount;
        payable.UpdatedBy = GetCurrentUserId();
        payable.LastModifiedDate = now;
        order.RefundedAmount += dto.Amount;
        order.RefundPendingAmount -= dto.Amount;
        order.RefundStatus = order.RefundPendingAmount == 0
            ? CustomerReturnRefundStatus.Refunded
            : CustomerReturnRefundStatus.PartiallyRefunded;
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = now;
        await _context.DebtTransactions.AddAsync(new DebtTransaction
        {
            PartyDebt = payable,
            TransactionType = LookupCodes.DebtTransactionType.Payment,
            Amount = dto.Amount,
            BalanceAfter = payable.CurrentBalance,
            RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
            RefId = order.Id,
            TransactionDate = now,
            DeduplicationKey = deduplicationKey,
            Note = $"Hoàn tiền đơn {order.ReturnCode}; tham chiếu {paymentReference}. {dto.Note}".Trim(),
            CreatedBy = GetCurrentUserId(),
            CreatedDate = now
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ApiResponse.Success(message: "Đã ghi nhận giao dịch hoàn tiền.");
    }

    public async Task<ApiResponse> CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("CANCEL", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse.BadRequest(message: "Lý do huỷ đơn hàng không được để trống.");

        var orgId = await GetCurrentOrganizationIdAsync();
        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == orgId && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed ||
            order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Received ||
            order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Inspected)
            return ApiResponse.UnprocessableEntity(message: "Không thể huỷ đơn sau khi kho đã nhận hàng. Hãy xử lý disposition cho hàng thực nhận.");

        if (order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Cancelled)
            return ApiResponse.BadRequest(message: "Đơn trả hàng đã ở trạng thái huỷ trước đó.");

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Cancelled && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái CANCELLED.");

        order.CustomerReturnOrderStatusId = status.Id;
        order.CancellationReason = reason.Trim();
        order.CancelledAt = DateTimeHelper.VietnamNow();
        order.CancelledByUserId = GetCurrentUserId();
        order.Note = (string.IsNullOrEmpty(order.Note) ? "" : order.Note + " | ") + "Huỷ đơn: " + reason.Trim();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Huỷ đơn trả hàng thành công.");
    }
}

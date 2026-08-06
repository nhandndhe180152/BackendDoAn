using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public CustomerReturnOrderService(
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CustomerReturnOrderService> logger,
        IScheduledJobService? scheduledJobService = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _scheduledJobService = scheduledJobService;
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

        if (action == LookupCodes.Action.Approve || action == LookupCodes.Action.Confirm || action == LookupCodes.Action.Cancel || action == LookupCodes.Action.Preview)
        {
            return roles.Contains(LookupCodes.Role.WarehouseOwner);
        }
        if (action == LookupCodes.Action.Inspect)
        {
            return roles.Contains(LookupCodes.Role.WarehouseStaff);
        }
        if (action == LookupCodes.Action.Create || action == LookupCodes.Action.Update)
        {
            return roles.Contains(LookupCodes.Role.SalesStaff) || roles.Contains(LookupCodes.Role.WarehouseOwner);
        }

        if (roles.Contains(LookupCodes.Role.EndUser)) return true;

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
            .AnyAsync(x => x.Id == dto.CustomerId && x.IsActive && !x.IsDeleted, cancellationToken);
        if (!customerExists)
            return ApiResponse.BadRequest(message: "Khách hàng không tồn tại hoặc đã ngừng hoạt động.");

        OutboundOrder? outbound = null;
        if (dto.OutboundOrderId.HasValue)
        {
            outbound = await _context.OutboundOrders
                .Include(o => o.OutboundOrderStatus)
                .Include(o => o.SalesOrder)
                .FirstOrDefaultAsync(o => o.Id == dto.OutboundOrderId.Value && !o.IsDeleted, cancellationToken);

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

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Draft && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái DRAFT cho đơn trả hàng.");

        var orgId = await GetCurrentOrganizationIdAsync();
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

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items)
                .ThenInclude(i => i.Allocations)
            .FirstOrDefaultAsync(o => o.Id == dto.Id && !o.IsDeleted, cancellationToken);

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
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

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
                    Note = alloc.Note
                });
            }

            dto.Items.Add(itemDto);
        }

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(CustomerReturnOrderPagedQuery query, CancellationToken cancellationToken = default)
    {
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
            .Where(o => !o.IsDeleted);

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

    public async Task<ApiResponse> ApproveAsync(int id, string? note, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("APPROVE", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Draft)
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

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == dto.Id && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code != CustomerReturnOrderStatusNames.Approved)
            return ApiResponse.UnprocessableEntity(message: "Chỉ được kiểm định đơn hàng đang ở trạng thái APPROVED.");

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
                if (checkSum != alloc.QuantityReturned)
                    return ApiResponse.BadRequest(message: $"Tổng số lượng phân loại ({checkSum:N3} kg) của phân bổ Id = {alloc.Id} phải bằng số lượng trả về ban đầu ({alloc.QuantityReturned:N3} kg).");

                if (allocDto.CreditQuantity > (allocDto.QuantityGood + allocDto.QuantityDamaged))
                    return ApiResponse.BadRequest(message: $"Số lượng hoàn tiền ({allocDto.CreditQuantity:N3} kg) không được vượt quá tổng số lượng nhận lại (Good + Damaged = {allocDto.QuantityGood + allocDto.QuantityDamaged:N3} kg).");

                if ((allocDto.QuantityDamaged > 0 || allocDto.QuantityRejected > 0)
                    && string.IsNullOrWhiteSpace(itemDto.DamageReason))
                {
                    return ApiResponse.BadRequest(message: "Phải nhập lý do đối với hàng hỏng hoặc không nhập lại kho.");
                }

                // Location Validation
                if (allocDto.QuantityGood > 0)
                {
                    if (!allocDto.RestockLocationId.HasValue)
                        return ApiResponse.BadRequest(message: "Phải chỉ định Restock Location cho hàng chất lượng Tốt (Good).");

                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == allocDto.RestockLocationId.Value && !l.IsDeleted && l.IsActive, cancellationToken);
                    if (loc == null || loc.WarehouseId != order.WarehouseId || loc.IsQuarantine)
                        return ApiResponse.BadRequest(message: "Restock Location không hợp lệ hoặc không thuộc kho của đơn hàng hoặc là khu cách ly.");
                }

                if (allocDto.QuantityDamaged > 0)
                {
                    if (!allocDto.QuarantineLocationId.HasValue)
                        return ApiResponse.BadRequest(message: "Phải chỉ định Quarantine Location cho hàng hỏng/lỗi (Damaged).");

                    var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == allocDto.QuarantineLocationId.Value && !l.IsDeleted && l.IsActive, cancellationToken);
                    if (loc == null || loc.WarehouseId != order.WarehouseId || !loc.IsQuarantine)
                        return ApiResponse.BadRequest(message: "Quarantine Location không hợp lệ hoặc không thuộc khu cách ly (IsQuarantine = true) của kho.");
                }

                alloc.QuantityGood = allocDto.QuantityGood;
                alloc.QuantityDamaged = allocDto.QuantityDamaged;
                alloc.QuantityRejected = allocDto.QuantityRejected;
                alloc.CreditQuantity = allocDto.CreditQuantity;
                alloc.RestockLocationId = allocDto.RestockLocationId;
                alloc.QuarantineLocationId = allocDto.QuarantineLocationId;
                alloc.CreditAmount = allocDto.CreditQuantity * alloc.UnitCreditPrice;
                alloc.Note = allocDto.Note;
                alloc.UpdatedBy = GetCurrentUserId();
                alloc.LastModifiedDate = DateTimeHelper.VietnamNow();

                totalGood += allocDto.QuantityGood;
                totalDamaged += allocDto.QuantityDamaged;
            }

            item.QuantityGood = totalGood;
            item.QuantityDamaged = totalDamaged;
        }

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Inspected && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái INSPECTED.");

        order.CustomerReturnOrderStatusId = status.Id;
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

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

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

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.OutboundOrderItemAllocation)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.PaddyLot)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

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
            }

            // 1. Process Restock (Good) allocations
            var goodAllocations = allocations.Where(a => a.QuantityGood > 0).ToList();
            foreach (var alloc in goodAllocations)
            {
                var locationId = alloc.RestockLocationId!.Value;

                // Atomic UPDATE location with capacity limit check
                var affected = await _context.ExecuteSqlRawAsync(
                    "UPDATE Location SET CurrentOccupancy = CurrentOccupancy + {0}, CurrentProductVariantId = {1}, LastModifiedDate = {2}, UpdatedBy = {3} " +
                    "WHERE Id = {4} AND WarehouseId = {5} AND IsActive = 1 AND IsDeleted = 0 AND (MaxCapacity IS NULL OR CurrentOccupancy + {0} <= MaxCapacity) AND IsQuarantine = 0",
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
            }

            // 2. Process Quarantine (Damaged) allocations
            var damagedAllocations = allocations.Where(a => a.QuantityDamaged > 0).ToList();
            foreach (var alloc in damagedAllocations)
            {
                var locationId = alloc.QuarantineLocationId!.Value;

                // Atomic UPDATE location with capacity limit check
                var affected = await _context.ExecuteSqlRawAsync(
                    "UPDATE Location SET CurrentOccupancy = CurrentOccupancy + {0}, CurrentProductVariantId = {1}, LastModifiedDate = {2}, UpdatedBy = {3} " +
                    "WHERE Id = {4} AND WarehouseId = {5} AND IsActive = 1 AND IsDeleted = 0 AND (MaxCapacity IS NULL OR CurrentOccupancy + {0} <= MaxCapacity) AND IsQuarantine = 1",
                    new object[] { alloc.QuantityDamaged, alloc.ProductVariantId, now, userId, locationId, order.WarehouseId }, cancellationToken);

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
                        QuantityOnHand = alloc.QuantityDamaged,
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    await _context.Inventories.AddAsync(inventory, cancellationToken);
                }
                else
                {
                    beforeQty = inventory.QuantityOnHand;
                    inventory.QuantityOnHand += alloc.QuantityDamaged;
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
                    TransactionType = InventoryTransactionTypeConstants.CustomerReturnQuarantine,
                    BeforeQuantity = beforeQty,
                    Quantity = alloc.QuantityDamaged,
                    AfterQuantity = beforeQty + alloc.QuantityDamaged,
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
                    var addedWeight = allocations.Where(a => a.PaddyLotId == lotId).Sum(a => a.QuantityGood + a.QuantityDamaged);
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

            order.UpdatedBy = userId;
            order.LastModifiedDate = now;

            await _context.SaveChangesAsync(cancellationToken);
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

    public async Task<ApiResponse> CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (!await CheckPermissionAsync("CANCEL", cancellationToken))
            return ApiResponse.Forbidden(message: "Bạn không có quyền thực hiện hành động này.");

        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse.BadRequest(message: "Lý do huỷ đơn hàng không được để trống.");

        var order = await _context.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderStatus)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

        if (order == null)
            return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Confirmed)
            return ApiResponse.UnprocessableEntity(message: "Không thể huỷ đơn trả hàng đã xác nhận thành công.");

        if (order.CustomerReturnOrderStatus.Code == CustomerReturnOrderStatusNames.Cancelled)
            return ApiResponse.BadRequest(message: "Đơn trả hàng đã ở trạng thái huỷ trước đó.");

        var status = await _context.CustomerReturnOrderStatuses
            .FirstOrDefaultAsync(s => s.Code == CustomerReturnOrderStatusNames.Cancelled && !s.IsDeleted, cancellationToken);
        if (status == null)
            return ApiResponse.Error(message: "Hệ thống chưa cấu hình trạng thái CANCELLED.");

        order.CustomerReturnOrderStatusId = status.Id;
        order.Note = (string.IsNullOrEmpty(order.Note) ? "" : order.Note + " | ") + "Huỷ đơn: " + reason.Trim();
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Huỷ đơn trả hàng thành công.");
    }
}

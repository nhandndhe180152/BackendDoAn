using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.PurchaseOrders;
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
/// Nghiệp vụ đơn mua hàng non-paddy (PurchaseOrder).
/// Flow: DRAFT → CONFIRMED → PARTIALLY_RECEIVED → RECEIVED
/// CANCELLED là nhánh kết thúc thay thế.
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IRepositoryBase<PurchaseOrder, int>     _purchaseOrderRepository;
    private readonly IRepositoryBase<PurchaseOrderItem, int> _purchaseOrderItemRepository;
    private readonly IRepositoryBase<PurchaseOrderStatus, int> _purchaseOrderStatusRepository;
    private readonly IRepositoryBase<InboundOrder, int>      _inboundOrderRepository;
    private readonly IRepositoryBase<InboundOrderItem, int>  _inboundOrderItemRepository;
    private readonly IRepositoryBase<InboundOrderStatus, int> _inboundOrderStatusRepository;
    private readonly IRepositoryBase<Supplier, int>          _supplierRepository;
    private readonly IRepositoryBase<ProductVariant, int>    _productVariantRepository;
    private readonly IHttpContextAccessor                     _httpContextAccessor;
    private readonly INotificationDispatcher                   _notificationDispatcher;

    public PurchaseOrderService(
        IRepositoryBase<PurchaseOrder, int>      purchaseOrderRepository,
        IRepositoryBase<PurchaseOrderItem, int>  purchaseOrderItemRepository,
        IRepositoryBase<PurchaseOrderStatus, int> purchaseOrderStatusRepository,
        IRepositoryBase<InboundOrder, int>       inboundOrderRepository,
        IRepositoryBase<InboundOrderItem, int>   inboundOrderItemRepository,
        IRepositoryBase<InboundOrderStatus, int> inboundOrderStatusRepository,
        IRepositoryBase<Supplier, int>           supplierRepository,
        IRepositoryBase<ProductVariant, int>     productVariantRepository,
        IHttpContextAccessor                      httpContextAccessor,
        INotificationDispatcher                   notificationDispatcher)
    {
        _purchaseOrderRepository       = purchaseOrderRepository;
        _purchaseOrderItemRepository   = purchaseOrderItemRepository;
        _purchaseOrderStatusRepository = purchaseOrderStatusRepository;
        _inboundOrderRepository        = inboundOrderRepository;
        _inboundOrderItemRepository    = inboundOrderItemRepository;
        _inboundOrderStatusRepository  = inboundOrderStatusRepository;
        _supplierRepository            = supplierRepository;
        _productVariantRepository      = productVariantRepository;
        _httpContextAccessor           = httpContextAccessor;
        _notificationDispatcher        = notificationDispatcher;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;

    private async Task<int> GetPoStatusIdAsync(string name)
    {
        var s = await _purchaseOrderStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"PurchaseOrderStatus '{name}' not found.");
    }

    private async Task<int> GetInboundStatusIdAsync(string name)
    {
        var s = await _inboundOrderStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return s?.Id ?? throw new InvalidOperationException($"InboundOrderStatus '{name}' not found.");
    }

    private async Task<PurchaseOrder?> LoadDetailAsync(int id)
    {
        return await _purchaseOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false,
                x => x.Status,
                x => x.Supplier,
                x => x.Warehouse,
                x => x.PurchaseOrderItems,
                x => x.InboundOrders)
            .Include(x => x.PurchaseOrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(x => x.InboundOrders)
                .ThenInclude(io => io.InboundOrderStatus)
            .FirstOrDefaultAsync();
    }

    private static PurchaseOrderDetailDto MapDetail(PurchaseOrder po)
    {
        // Tính tổng đã nhận per PurchaseOrderItem từ các InboundOrderItem
        var receivedByPoItem = po.InboundOrders
            .Where(io => !io.IsDeleted)
            .SelectMany(io => io.InboundOrderItems)
            .Where(ii => !ii.IsDeleted && ii.PurchaseOrderItemId.HasValue)
            .GroupBy(ii => ii.PurchaseOrderItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(ii => ii.QuantityReceived));

        return new PurchaseOrderDetailDto
        {
            Id             = po.Id,
            POCode         = po.POCode,
            SupplierId     = po.SupplierId,
            SupplierName   = po.Supplier?.Name ?? "",
            StatusId       = po.StatusId,
            StatusName     = po.Status?.Name ?? "",
            StatusColor    = po.Status?.Color ?? "",
            WarehouseId    = po.WarehouseId,
            WarehouseName  = po.Warehouse?.Name,
            OrderDate      = po.OrderDate,
            ExpectedDate   = po.ExpectedDate,
            TotalAmount    = po.TotalAmount,
            Note           = po.Note,
            CreatedDate    = po.CreatedDate,
            Items = po.PurchaseOrderItems.Where(i => !i.IsDeleted).Select(i =>
            {
                receivedByPoItem.TryGetValue(i.Id, out var received);
                return new PurchaseOrderItemDto
                {
                    Id                 = i.Id,
                    ProductVariantId   = i.ProductVariantId,
                    ProductVariantName = i.ProductVariant?.Name ?? "",
                    SKU                = i.ProductVariant?.SKU,
                    QuantityOrdered    = i.QuantityOrdered,
                    QuantityReceived   = received,
                    QuantityRemaining  = Math.Max(0, i.QuantityOrdered - received),
                    UnitCostPrice      = i.UnitCostPrice,
                    LineAmount         = i.LineAmount,
                    Note               = i.Note
                };
            }).ToList(),
            InboundOrders = po.InboundOrders.Where(io => !io.IsDeleted).Select(io =>
                new PurchaseOrderInboundSummaryDto
                {
                    Id                = io.Id,
                    InboundStatusName = io.InboundOrderStatus?.Name ?? "",
                    CompletedDate     = io.CompletedDate
                }).ToList()
        };
    }

    // ── Queries ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetPagedAsync(PurchaseOrderPagedQuery query)
    {
        var baseQuery = _purchaseOrderRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Status, x => x.Supplier, x => x.Warehouse);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.ToLower();
            baseQuery = baseQuery.Where(x =>
                x.POCode.ToLower().Contains(kw) ||
                x.Supplier.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        var total = await baseQuery.CountAsync();
        var skip  = (query.Page - 1) * query.PageSize;

        var list = await baseQuery
            .OrderByDescending(x => x.CreatedDate)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(po => new PurchaseOrderListDto
            {
                Id            = po.Id,
                POCode        = po.POCode,
                SupplierId    = po.SupplierId,
                SupplierName  = po.Supplier.Name,
                StatusId      = po.StatusId,
                StatusName    = po.Status.Name,
                StatusColor   = po.Status.Color,
                WarehouseId   = po.WarehouseId,
                WarehouseName = po.Warehouse != null ? po.Warehouse.Name : null,
                OrderDate     = po.OrderDate,
                ExpectedDate  = po.ExpectedDate,
                TotalAmount   = po.TotalAmount,
                Note          = po.Note,
                CreatedDate   = po.CreatedDate
            })
            .ToListAsync();

        return ApiResponse.Success(new { Total = total, Items = list });
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var po = await LoadDetailAsync(id);
        if (po == null)
            return ApiResponse.NotFound("Không tìm thấy đơn mua.", ApiCodeConstants.PurchaseOrder.NotFound);

        return ApiResponse.Success(MapDetail(po));
    }

    // ── Commands ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> CreateAsync(CreatePurchaseOrderDto dto)
    {
        if (!dto.Items.Any())
            return ApiResponse.BadRequest("Đơn mua phải có ít nhất 1 dòng sản phẩm.", ApiCodeConstants.PurchaseOrder.InvalidRequest);

        // Validate supplier
        var supplier = await _supplierRepository.FirstOrDefaultAsync(x => x.Id == dto.SupplierId && !x.IsDeleted && x.IsActive);
        if (supplier == null)
            return ApiResponse.UnprocessableEntity("Nhà cung cấp không tồn tại hoặc đã bị khóa.", ApiCodeConstants.PurchaseOrder.InvalidRequest);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        // Generate POCode: PO-YYYYMMDD-XXXX
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"PO-{datePart}";
        var count    = await _purchaseOrderRepository
            .FindByCondition(x => x.POCode.StartsWith(baseCode))
            .CountAsync();
        var poCode = $"{baseCode}-{(count + 1):D4}";

        // L2: Tránh trùng mã PO do race condition
        int attempts = 0;
        while (await _purchaseOrderRepository.AnyAsync(x => x.POCode == poCode && x.OrganizationId == dto.OrganizationId) && attempts < 10)
        {
            attempts++;
            poCode = $"{baseCode}-{(count + 1 + attempts):D4}";
        }

        var statusId = await GetPoStatusIdAsync(PurchaseOrderStatusNames.Draft);

        decimal totalAmount = 0;
        var items = new List<PurchaseOrderItem>();

        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityOrdered <= 0)
                return ApiResponse.BadRequest($"Số lượng sản phẩm {itemDto.ProductVariantId} phải > 0.", ApiCodeConstants.PurchaseOrder.InvalidRequest);

            // Validate variant
            var pv = await _productVariantRepository.FirstOrDefaultAsync(x => x.Id == itemDto.ProductVariantId && !x.IsDeleted && x.IsActive);
            if (pv == null)
                return ApiResponse.UnprocessableEntity($"Sản phẩm variant ID {itemDto.ProductVariantId} không tồn tại hoặc bị khóa.", ApiCodeConstants.PurchaseOrder.InvalidRequest);

            var lineAmount = itemDto.QuantityOrdered * itemDto.UnitCostPrice;
            totalAmount += lineAmount;

            items.Add(new PurchaseOrderItem
            {
                ProductVariantId = itemDto.ProductVariantId,
                QuantityOrdered  = itemDto.QuantityOrdered,
                UnitCostPrice    = itemDto.UnitCostPrice,
                LineAmount       = lineAmount,
                Note             = itemDto.Note,
                CreatedDate      = now,
                CreatedBy        = userId
            });
        }

        var po = new PurchaseOrder
        {
            OrganizationId   = dto.OrganizationId,
            POCode           = poCode,
            SupplierId       = dto.SupplierId,
            StatusId         = statusId,
            WarehouseId      = dto.WarehouseId,
            OrderDate        = now,
            ExpectedDate     = dto.ExpectedDate,
            TotalAmount      = totalAmount,
            Note             = dto.Note,
            CreatedDate      = now,
            CreatedBy        = userId,
            PurchaseOrderItems = items
        };

        await _purchaseOrderRepository.CreateAsync(po);
        await _purchaseOrderRepository.SaveChangesAsync();

        return ApiResponse.Created(new { Id = po.Id, POCode = poCode, TotalAmount = totalAmount },
            "Tạo đơn mua thành công.");
    }

    public async Task<ApiResponse> UpdateAsync(UpdatePurchaseOrderDto dto)
    {
        var po = await LoadDetailAsync(dto.Id);
        if (po == null)
            return ApiResponse.NotFound("Không tìm thấy đơn mua.", ApiCodeConstants.PurchaseOrder.NotFound);

        if (po.Status?.Name != PurchaseOrderStatusNames.Draft)
            return ApiResponse.Conflict("Chỉ có thể chỉnh sửa đơn mua ở trạng thái Draft.",
                ApiCodeConstants.PurchaseOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        po.ExpectedDate      = dto.ExpectedDate;
        po.Note              = dto.Note;
        po.LastModifiedDate  = now;
        po.UpdatedBy         = userId;

        if (dto.Items.Any())
        {
            // Xóa items cũ
            foreach (var old in po.PurchaseOrderItems.ToList())
            {
                old.IsDeleted = true;
                await _purchaseOrderItemRepository.UpdateAsync(old);
            }

            decimal total = 0;
            foreach (var itemDto in dto.Items)
            {
                var lineAmount = itemDto.QuantityOrdered * itemDto.UnitCostPrice;
                total += lineAmount;
                await _purchaseOrderItemRepository.CreateAsync(new PurchaseOrderItem
                {
                    PurchaseOrderId  = po.Id,
                    ProductVariantId = itemDto.ProductVariantId,
                    QuantityOrdered  = itemDto.QuantityOrdered,
                    UnitCostPrice    = itemDto.UnitCostPrice,
                    LineAmount       = lineAmount,
                    Note             = itemDto.Note,
                    CreatedDate      = now,
                    CreatedBy        = userId
                });
            }
            po.TotalAmount = total;
        }

        await _purchaseOrderRepository.UpdateAsync(po);
        await _purchaseOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(message: "Cập nhật đơn mua thành công.");
    }

    public async Task<ApiResponse> ConfirmAsync(int id)
    {
        var po = await LoadDetailAsync(id);
        if (po == null)
            return ApiResponse.NotFound("Không tìm thấy đơn mua.", ApiCodeConstants.PurchaseOrder.NotFound);

        if (po.Status?.Name != PurchaseOrderStatusNames.Draft)
            return ApiResponse.Conflict(
                $"Đơn mua đang ở trạng thái '{po.Status?.Name}', không thể xác nhận.",
                ApiCodeConstants.PurchaseOrder.InvalidState);

        var now = DateTimeHelper.VietnamNow();
        po.StatusId         = await GetPoStatusIdAsync(PurchaseOrderStatusNames.Confirmed);
        po.LastModifiedDate = now;
        po.UpdatedBy        = GetCurrentUserId();

        await _purchaseOrderRepository.UpdateAsync(po);
        await _purchaseOrderRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên thu mua, Chủ kho, Nhân viên kho: đơn mua đã xác nhận.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.PurchaseOrderConfirmed,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.PURCHASING, CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE } },
            new object[] { po.POCode },
            "/admin/purchase-orders",
            GetCurrentUserId());

        return ApiResponse.Success(message: "Đơn mua đã được xác nhận.");
    }

    public async Task<ApiResponse> CreateInboundAsync(int id)
    {
        var po = await LoadDetailAsync(id);
        if (po == null)
            return ApiResponse.NotFound("Không tìm thấy đơn mua.", ApiCodeConstants.PurchaseOrder.NotFound);

        var allowedStates = new[]
        {
            PurchaseOrderStatusNames.Confirmed,
            PurchaseOrderStatusNames.PartiallyReceived
        };
        if (!allowedStates.Contains(po.Status?.Name))
            return ApiResponse.Conflict(
                $"Đơn mua phải ở trạng thái Confirmed hoặc PartiallyReceived để tạo phiếu nhập. Hiện tại: '{po.Status?.Name}'.",
                ApiCodeConstants.PurchaseOrder.InvalidState);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        var draftStatusId = await GetInboundStatusIdAsync(InboundOrderStatusNames.Draft);

        // Tính tổng đã nhận per PO item từ các InboundOrder trước
        var receivedByPoItem = po.InboundOrders
            .Where(io => !io.IsDeleted)
            .SelectMany(io => io.InboundOrderItems)
            .Where(ii => !ii.IsDeleted && ii.PurchaseOrderItemId.HasValue)
            .GroupBy(ii => ii.PurchaseOrderItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(ii => ii.QuantityReceived));

        // Chỉ tạo InboundOrderItem cho những PO item còn thiếu
        var pendingItems = po.PurchaseOrderItems
            .Where(i => !i.IsDeleted)
            .Select(i =>
            {
                receivedByPoItem.TryGetValue(i.Id, out var received);
                return new { Item = i, Remaining = i.QuantityOrdered - received };
            })
            .Where(x => x.Remaining > 0)
            .ToList();

        if (!pendingItems.Any())
            return ApiResponse.Conflict("Tất cả sản phẩm trong đơn mua đã được nhận đủ.",
                ApiCodeConstants.PurchaseOrder.InvalidState);

        // Generate phiếu nhập code
        var datePart  = now.ToString("yyyyMMdd");
        var baseCode  = $"INB-{datePart}";
        var cntToday  = await _inboundOrderRepository
            .FindByCondition(x => x.POCode != null && x.POCode.StartsWith(baseCode))
            .CountAsync();
        var inbCode = $"{baseCode}-{(cntToday + 1):D4}";

        // L2: Tránh trùng mã phiếu nhập INB do race condition
        int attemptsInb = 0;
        while (await _inboundOrderRepository.AnyAsync(x => x.POCode == inbCode && x.OrganizationId == po.OrganizationId) && attemptsInb < 10)
        {
            attemptsInb++;
            inbCode = $"{baseCode}-{(cntToday + 1 + attemptsInb):D4}";
        }

        var inbound = new InboundOrder
        {
            WarehouseId          = po.WarehouseId ?? 0,
            SupplierId           = po.SupplierId,
            InboundOrderStatusId = draftStatusId,
            POCode               = inbCode,
            PurchaseOrderId      = po.Id,
            SourceType           = "PO",
            OrganizationId       = po.OrganizationId,
            ExpectedDate         = po.ExpectedDate,
            Note                 = $"Tạo từ đơn mua {po.POCode}",
            TotalAssetValue      = 0,
            CreatedDate          = now,
            CreatedBy            = userId
        };

        foreach (var pending in pendingItems)
        {
            inbound.InboundOrderItems.Add(new InboundOrderItem
            {
                ProductVariantId    = pending.Item.ProductVariantId,
                QuantityOrdered     = pending.Remaining,
                QuantityReceived    = 0,
                UnitCostPrice       = pending.Item.UnitCostPrice,
                PurchaseOrderItemId = pending.Item.Id,
                Note                = pending.Item.Note,
                CreatedDate         = now,
                CreatedBy           = userId
            });
        }

        await _inboundOrderRepository.CreateAsync(inbound);
        await _purchaseOrderRepository.SaveChangesAsync();

        return ApiResponse.Created(new { InboundOrderId = inbound.Id, POCode = inbCode },
            "Tạo phiếu nhập kho thành công.");
    }

    public async Task<ApiResponse> CancelAsync(int id)
    {
        var po = await LoadDetailAsync(id);
        if (po == null)
            return ApiResponse.NotFound("Không tìm thấy đơn mua.", ApiCodeConstants.PurchaseOrder.NotFound);

        var cancellableStates = new[]
        {
            PurchaseOrderStatusNames.Draft,
            PurchaseOrderStatusNames.Confirmed
        };
        if (!cancellableStates.Contains(po.Status?.Name))
            return ApiResponse.Conflict(
                $"Không thể hủy đơn mua ở trạng thái '{po.Status?.Name}'.",
                ApiCodeConstants.PurchaseOrder.InvalidState);

        var now = DateTimeHelper.VietnamNow();
        po.StatusId         = await GetPoStatusIdAsync(PurchaseOrderStatusNames.Cancelled);
        po.LastModifiedDate = now;
        po.UpdatedBy        = GetCurrentUserId();

        await _purchaseOrderRepository.UpdateAsync(po);
        await _purchaseOrderRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên thu mua và Chủ kho: đơn mua đã bị hủy.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.PurchaseOrderCancelled,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.PURCHASING, CommonConstants.Role.OWNER } },
            new object[] { po.POCode },
            "/admin/purchase-orders",
            GetCurrentUserId());

        return ApiResponse.Success(message: "Đơn mua đã được hủy.");
    }
}

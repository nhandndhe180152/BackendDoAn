using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Share.Services;
using Backend.Application.Constants;
using Backend.Application.DTOs.ReturnToSuppliers;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ đơn trả hàng về nhà cung cấp (FE-16).
/// Flow: Chờ duyệt → Đã duyệt → Hoàn thành (hoặc Đã huỷ).
/// ConfirmAsync xuất hàng khỏi vị trí cách ly, giảm tồn lô và đảo ngược công nợ phải trả NCC.
/// Ghi chú: bảng trạng thái chưa có seed nên service tự tạo (lazy-seed) khi cần.
/// </summary>
public class ReturnToSupplierOrderService : IReturnToSupplierOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ReturnToSupplierOrderService> _logger;

    private readonly IScheduledJobService? _scheduledJobService;

    public ReturnToSupplierOrderService(
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ReturnToSupplierOrderService> logger,
        IScheduledJobService? scheduledJobService = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _scheduledJobService = scheduledJobService;
    }

    private int GetCurrentUserId() => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 1;

    /// <summary>Lazy-seed trạng thái (bảng ReturnToSupplierOrderStatus chưa có dữ liệu seed).</summary>
    private async Task<ReturnToSupplierOrderStatus> EnsureStatusAsync(string code, string name, string color, CancellationToken ct)
    {
        var st = await _context.ReturnToSupplierOrderStatuses
            .FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted, ct);
        if (st == null)
        {
            st = new ReturnToSupplierOrderStatus
            {
                Code = code,
                Name = name,
                Color = color,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTimeHelper.VietnamNow()
            };
            await _context.ReturnToSupplierOrderStatuses.AddAsync(st, ct);
            await _context.SaveChangesAsync(ct);
        }
        return st;
    }

    public async Task<ApiResponse> CreateAsync(CreateReturnToSupplierOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.WarehouseId <= 0 || dto.SupplierId <= 0)
            return ApiResponse.BadRequest(message: "Thiếu kho hoặc nhà cung cấp.");
        if (dto.Items == null || dto.Items.Count == 0)
            return ApiResponse.BadRequest(message: "Đơn trả hàng phải có ít nhất 1 dòng.");

        foreach (var it in dto.Items)
        {
            if (it.ProductVariantId <= 0 || it.QuarantineLocationId <= 0 || it.QuantityToReturn <= 0)
                return ApiResponse.BadRequest(message: "Mỗi dòng phải có sản phẩm, vị trí cách ly và số lượng > 0.");
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == dto.SupplierId && !x.IsDeleted, cancellationToken);
        if (supplier == null)
            return ApiResponse.NotFound(message: "Không tìm thấy nhà cung cấp.");

        var now = DateTimeHelper.VietnamNow();
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"RTS-{datePart}";
        var cnt = await _context.ReturnToSupplierOrders.CountAsync(x => x.ReturnCode.StartsWith(baseCode), cancellationToken);
        var returnCode = $"{baseCode}-{(cnt + 1):D6}";
        int attempts = 0;
        while (await _context.ReturnToSupplierOrders.AnyAsync(x => x.ReturnCode == returnCode, cancellationToken) && attempts < 10)
        {
            attempts++;
            returnCode = $"{baseCode}-{(cnt + 1 + attempts):D6}";
        }

        var draft = await EnsureStatusAsync(ReturnToSupplierOrderStatusNames.Draft, "Chờ duyệt", "#f59e0b", cancellationToken);

        var order = new ReturnToSupplierOrder
        {
            WarehouseId = dto.WarehouseId,
            SupplierId = dto.SupplierId,
            InboundOrderId = dto.InboundOrderId,
            ReturnToSupplierOrderStatusId = draft.Id,
            ReturnCode = returnCode,
            Note = dto.Note,
            CreatedBy = GetCurrentUserId(),
            CreatedDate = now
        };

        foreach (var it in dto.Items)
        {
            order.Items.Add(new ReturnToSupplierOrderItem
            {
                ProductVariantId = it.ProductVariantId,
                QuarantineLocationId = it.QuarantineLocationId,
                QuantityToReturn = it.QuantityToReturn,
                QuantityActualReturned = 0,
                DamageReason = it.DamageReason,
                Note = it.Note,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = now
            });
        }

        await _context.ReturnToSupplierOrders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Created(order.Id, "Tạo đơn trả hàng nhà cung cấp thành công.");
    }

    public async Task<ApiResponse> ApproveAsync(int id, string? note, CancellationToken cancellationToken = default)
    {
        var order = await _context.ReturnToSupplierOrders
            .Include(o => o.ReturnToSupplierOrderStatus)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        if (order.ReturnToSupplierOrderStatus.Code != ReturnToSupplierOrderStatusNames.Draft)
            return ApiResponse.UnprocessableEntity("Chỉ duyệt đơn đang ở trạng thái Chờ duyệt.");

        var approved = await EnsureStatusAsync(ReturnToSupplierOrderStatusNames.Approved, "Đã duyệt", "#3b82f6", cancellationToken);
        order.ReturnToSupplierOrderStatusId = approved.Id;
        order.ApprovedDate = DateTimeHelper.VietnamNow();
        order.ApprovedBy = GetCurrentUserId();
        if (!string.IsNullOrWhiteSpace(note))
            order.Note = string.IsNullOrWhiteSpace(order.Note) ? note : $"{order.Note} | Duyệt: {note}";
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Duyệt đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> ConfirmAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _context.ReturnToSupplierOrders
            .Include(o => o.ReturnToSupplierOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        var curCode = order.ReturnToSupplierOrderStatus.Code;
        if (curCode == ReturnToSupplierOrderStatusNames.Completed)
            return ApiResponse.BadRequest(message: "Đơn trả hàng đã hoàn thành trước đó.", code: "RTS_ALREADY_COMPLETED");
        if (curCode != ReturnToSupplierOrderStatusNames.Draft && curCode != ReturnToSupplierOrderStatusNames.Approved)
            return ApiResponse.UnprocessableEntity("Chỉ xác nhận đơn đang ở trạng thái Chờ duyệt hoặc Đã duyệt.");

        var completed = await EnsureStatusAsync(ReturnToSupplierOrderStatusNames.Completed, "Hoàn thành", "#16a34a", cancellationToken);
        var now = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            decimal totalReturnValue = 0m;

            foreach (var item in order.Items)
            {
                var qty = item.QuantityActualReturned > 0 ? item.QuantityActualReturned : item.QuantityToReturn;
                if (qty <= 0) continue;
                if (!item.ProductVariantId.HasValue || !item.QuarantineLocationId.HasValue)
                    throw new InvalidOperationException("Dòng trả hàng thiếu sản phẩm hoặc vị trí cách ly.");

                var variantId = item.ProductVariantId.Value;
                var locId = item.QuarantineLocationId.Value;
                decimal qtyDec = qty;

                var invs = await _context.Inventories
                    .Where(x => !x.IsDeleted
                        && x.WarehouseId == order.WarehouseId
                        && x.LocationId == locId
                        && x.ProductVariantId == variantId
                        && x.QuantityOnHand > 0)
                    .OrderBy(x => x.CreatedDate)
                    .ToListAsync(cancellationToken);

                var totalAvailable = invs.Sum(x => x.QuantityOnHand - x.QuantityReserved);
                if (totalAvailable < qtyDec)
                    throw new InvalidOperationException(
                        $"Không đủ tồn khả dụng tại vị trí cách ly cho sản phẩm {variantId} (khả dụng {totalAvailable}, cần trả {qtyDec}).");

                decimal remaining = qtyDec;
                foreach (var inv in invs)
                {
                    if (remaining <= 0) break;
                    var avail = inv.QuantityOnHand - inv.QuantityReserved;
                    if (avail <= 0) continue;
                    var take = Math.Min(avail, remaining);

                    var before = inv.QuantityOnHand;
                    inv.QuantityOnHand -= take;
                    inv.LastModifiedDate = now;
                    inv.UpdatedBy = userId;

                    // Giảm tồn lô nếu hàng gắn lô
                    if (inv.PaddyLotId.HasValue)
                    {
                        var lot = await _context.PaddyLots.FirstOrDefaultAsync(l => l.Id == inv.PaddyLotId.Value && !l.IsDeleted, cancellationToken);
                        if (lot != null)
                        {
                            lot.RemainingWeightKg = Math.Max(0m, lot.RemainingWeightKg - take);
                            lot.LastModifiedDate = now;
                            lot.UpdatedBy = userId;
                        }
                    }

                    totalReturnValue += take * inv.CostPrice;

                    var returnTx = new InventoryTransaction
                    {
                        InventoryId = inv.Id,
                        WarehouseId = inv.WarehouseId,
                        LocationId = inv.LocationId,
                        ProductVariantId = inv.ProductVariantId,
                        PaddyLotId = inv.PaddyLotId,
                        TransactionType = InventoryTransactionTypeConstants.Export,
                        ReferenceType = "RETURN_TO_SUPPLIER",
                        ReferenceId = order.Id,
                        ReferenceItemId = item.Id,
                        Quantity = -take,
                        BeforeQuantity = before,
                        AfterQuantity = inv.QuantityOnHand,
                        WeightKg = take,
                        Note = $"Xuất trả nhà cung cấp (đơn {order.ReturnCode})",
                        CreatedBy = userId,
                        CreatedDate = now
                    };
                    // Quy đổi Before/After sang TỔNG TỒN CỦA CỘT (cộng tồn các dòng khác cùng vị trí).
                    if (returnTx.LocationId.HasValue)
                    {
                        var otherOnHand = await _context.Inventories
                            .Where(i => i.LocationId == returnTx.LocationId.Value && !i.IsDeleted && i.Id != inv.Id)
                            .SumAsync(i => i.QuantityOnHand, cancellationToken);
                        returnTx.BeforeQuantity += otherOnHand;
                        returnTx.AfterQuantity += otherOnHand;
                    }
                    await _context.InventoryTransactions.AddAsync(returnTx, cancellationToken);

                    remaining -= take;
                }

                item.QuantityActualReturned = qty;
                item.UpdatedBy = userId;
                item.LastModifiedDate = now;

                // Giảm sức chứa vị trí cách ly
                var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == locId && !l.IsDeleted, cancellationToken);
                if (loc != null)
                {
                    loc.CurrentOccupancy = Math.Max(0m, loc.CurrentOccupancy - qtyDec);
                    loc.LastModifiedDate = now;
                    loc.UpdatedBy = userId;
                }
            }

            PartyDebt? payable = null;
            decimal reduce = 0;
            if (totalReturnValue > 0)
            {
                payable = await _context.PartyDebts.FirstOrDefaultAsync(d =>
                    d.PartyType == LookupCodes.PartyType.Supplier &&
                    d.PartyId == order.SupplierId &&
                    d.Direction == LookupCodes.DebtDirection.Payable &&
                    d.IsActive && !d.IsDeleted, cancellationToken);

                if (payable != null && payable.CurrentBalance > 0)
                {
                    reduce = Math.Min(payable.CurrentBalance, totalReturnValue);
                    payable.CurrentBalance -= reduce;
                    payable.LastModifiedDate = now;
                    payable.UpdatedBy = userId;

                    await _context.DebtTransactions.AddAsync(new DebtTransaction
                    {
                        PartyDebtId = payable.Id,
                        TransactionType = LookupCodes.DebtTransactionType.ReturnCredit,
                        Amount = reduce,
                        BalanceAfter = payable.CurrentBalance,
                        RefType = "RETURN_TO_SUPPLIER",
                        RefId = order.Id,
                        TransactionDate = now,
                        Note = $"Giảm công nợ phải trả NCC do trả hàng (đơn {order.ReturnCode})",
                        DeduplicationKey = $"RTS-CONFIRM-{order.Id}",
                        CreatedBy = userId,
                        CreatedDate = now
                    }, cancellationToken);
                }
            }

            order.ReturnToSupplierOrderStatusId = completed.Id;
            order.CompletedDate = now;
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Enqueue targeted background job evaluation for JOB-04 after transaction completes
            if (_scheduledJobService != null && payable != null && reduce > 0)
            {
                _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(payable.Id, CancellationToken.None));
            }

            return ApiResponse.Success(message: "Xác nhận trả hàng nhà cung cấp thành công.");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi khi xác nhận đơn trả hàng NCC {OrderId}", id);
            throw;
        }
    }

    public async Task<ApiResponse> CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var order = await _context.ReturnToSupplierOrders
            .Include(o => o.ReturnToSupplierOrderStatus)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        var curCode = order.ReturnToSupplierOrderStatus.Code;
        if (curCode != ReturnToSupplierOrderStatusNames.Draft && curCode != ReturnToSupplierOrderStatusNames.Approved)
            return ApiResponse.UnprocessableEntity("Chỉ huỷ đơn đang ở trạng thái Chờ duyệt hoặc Đã duyệt.");

        var cancelled = await EnsureStatusAsync(ReturnToSupplierOrderStatusNames.Cancelled, "Đã huỷ", "#ef4444", cancellationToken);
        order.ReturnToSupplierOrderStatusId = cancelled.Id;
        order.Note = string.IsNullOrWhiteSpace(order.Note) ? $"Huỷ: {reason}" : $"{order.Note} | Huỷ: {reason}";
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Huỷ đơn trả hàng thành công.");
    }

    public async Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _context.ReturnToSupplierOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Supplier)
            .Include(o => o.ReturnToSupplierOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.ProductVariant)
            .Include(o => o.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.QuarantineLocation)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (order == null) return ApiResponse.NotFound(message: "Không tìm thấy đơn trả hàng.");

        return ApiResponse.Success(MapDetail(order));
    }

    public async Task<ApiResponse> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _context.ReturnToSupplierOrders
            .Where(o => !o.IsDeleted)
            .Include(o => o.Warehouse)
            .Include(o => o.Supplier)
            .Include(o => o.ReturnToSupplierOrderStatus)
            .Include(o => o.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.ProductVariant)
            .Include(o => o.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.QuarantineLocation)
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync(cancellationToken);

        return ApiResponse.Success(orders.Select(MapDetail).ToList());
    }

    private static ReturnToSupplierOrderDetailDto MapDetail(ReturnToSupplierOrder o) => new()
    {
        Id = o.Id,
        ReturnCode = o.ReturnCode,
        WarehouseId = o.WarehouseId,
        WarehouseName = o.Warehouse?.Name,
        SupplierId = o.SupplierId,
        SupplierName = o.Supplier?.Name,
        StatusId = o.ReturnToSupplierOrderStatusId,
        StatusName = o.ReturnToSupplierOrderStatus?.Name,
        StatusCode = o.ReturnToSupplierOrderStatus?.Code,
        InboundOrderId = o.InboundOrderId,
        Note = o.Note,
        ApprovedDate = o.ApprovedDate,
        CompletedDate = o.CompletedDate,
        CreatedDate = o.CreatedDate,
        Items = o.Items.Where(i => !i.IsDeleted).Select(i => new ReturnToSupplierOrderItemDto
        {
            Id = i.Id,
            ProductVariantId = i.ProductVariantId,
            ProductVariantName = i.ProductVariant?.Name,
            SKU = i.ProductVariant?.SKU,
            QuarantineLocationId = i.QuarantineLocationId,
            LocationCode = i.QuarantineLocation?.SlotCode,
            QuantityToReturn = i.QuantityToReturn,
            QuantityActualReturned = i.QuantityActualReturned,
            DamageReason = i.DamageReason,
            Note = i.Note
        }).ToList()
    };
}

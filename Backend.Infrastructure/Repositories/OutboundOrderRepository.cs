using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class OutboundOrderRepository : RepositoryBase<OutboundOrder, int>, IOutboundOrderRepository
{
    private readonly BackendContext _context;

    public OutboundOrderRepository(BackendContext context, IUnitOfWork unitOfWork)
        : base(context, unitOfWork)
    {
        _context = context;
    }

    /// <summary>
    /// Load OutboundOrder với đầy đủ allocation chain để phục vụ ConfirmDispatch.
    /// </summary>
    public async Task<OutboundOrder?> GetByIdDetailAsync(int id)
    {
        return await _context.OutboundOrders
            .Include(x => x.OutboundOrderStatus)
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Include(x => x.OutboundOrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(x => x.OutboundOrderItems)
                .ThenInclude(i => i.SalesOrderItem)
            .Include(x => x.OutboundOrderItems)
                .ThenInclude(i => i.Allocations)
                    .ThenInclude(a => a.Inventory)
            .Include(x => x.OutboundOrderItems)
                .ThenInclude(i => i.Allocations)
                    .ThenInclude(a => a.PaddyLot)
                        .ThenInclude(pl => pl!.Status)
            .Include(x => x.OutboundOrderItems)
                .ThenInclude(i => i.Allocations)
                    .ThenInclude(a => a.Location)
            // Tách thành nhiều SELECT theo từng collection để tránh nổ tích Descartes
            // (OutboundOrderItems × Allocations × ...). Kết quả trả về không đổi.
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }

    public async Task<List<OutboundOrder>> GetPagedListAsync(
        string? keyword, int skip, int take, int? outboundStatusId = null,
        int? salesOrderId = null, int? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.OutboundOrders
            .Include(x => x.OutboundOrderStatus)
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Where(x => !x.IsDeleted);

        query = ApplyFilters(query, keyword, outboundStatusId, salesOrderId, warehouseId, fromDate, toDate);

        return await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountAsync(
        string? keyword, int? outboundStatusId = null,
        int? salesOrderId = null, int? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.OutboundOrders.Where(x => !x.IsDeleted);

        query = ApplyFilters(query, keyword, outboundStatusId, salesOrderId, warehouseId, fromDate, toDate);
        return await query.CountAsync();
    }

    /// <summary>
    /// Dùng chung cho Count và GetPagedList để tổng số bản ghi luôn khớp với
    /// dữ liệu trả về — nếu hai bên lọc khác nhau thì phân trang sẽ sai.
    /// </summary>
    private static IQueryable<OutboundOrder> ApplyFilters(
        IQueryable<OutboundOrder> query, string? keyword, int? outboundStatusId,
        int? salesOrderId = null, int? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            bool isNumber = int.TryParse(kw, out var idVal);

            query = query.Where(x =>
                (isNumber && x.Id == idVal) ||
                x.SalesOrder.SOCode.ToLower().Contains(kw) ||
                x.SalesOrder.Customer.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        if (outboundStatusId.HasValue && outboundStatusId.Value > 0)
        {
            query = query.Where(x => x.OutboundOrderStatusId == outboundStatusId.Value);
        }

        if (salesOrderId.HasValue && salesOrderId.Value > 0)
        {
            query = query.Where(x => x.SalesOrderId == salesOrderId.Value);
        }

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= fromDate.Value || (x.CompletedDate.HasValue && x.CompletedDate.Value >= fromDate.Value));
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate <= toDate.Value || (x.CompletedDate.HasValue && x.CompletedDate.Value <= toDate.Value));
        }

        return query;
    }
}

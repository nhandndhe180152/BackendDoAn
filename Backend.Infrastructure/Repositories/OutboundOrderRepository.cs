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
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }

    public async Task<List<OutboundOrder>> GetPagedListAsync(string? keyword, int skip, int take)
    {
        var query = _context.OutboundOrders
            .Include(x => x.OutboundOrderStatus)
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrder)
                .ThenInclude(so => so.Customer)
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(x =>
                x.SalesOrder.SOCode.ToLower().Contains(kw) ||
                x.SalesOrder.Customer.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        return await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountAsync(string? keyword)
    {
        var query = _context.OutboundOrders.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(x =>
                x.SalesOrder.SOCode.ToLower().Contains(kw) ||
                x.SalesOrder.Customer.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        return await query.CountAsync();
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class SalesOrderRepository : RepositoryBase<SalesOrder, int>, ISalesOrderRepository
{
    private readonly BackendContext _context;

    public SalesOrderRepository(BackendContext context, IUnitOfWork unitOfWork)
        : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<SalesOrder?> GetByIdDetailAsync(int id)
    {
        return await _context.SalesOrders
            .Include(x => x.Customer)
            .Include(x => x.Status)
            .Include(x => x.Warehouse)
            .Include(x => x.Organization)
            .Include(x => x.SalesOrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(x => x.OutboundOrders)
                .ThenInclude(o => o.OutboundOrderStatus)
            .Include(x => x.MillingOrders)
                .ThenInclude(o => o.Status)
            // Tách thành nhiều SELECT theo từng collection để tránh nổ tích Descartes
            // (SalesOrderItems × OutboundOrders). Kết quả trả về không đổi.
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }

    public async Task<List<SalesOrder>> GetPagedListAsync(string? keyword, int skip, int take)
    {
        var query = _context.SalesOrders
            .Include(x => x.Customer)
            .Include(x => x.Status)
            .Include(x => x.Warehouse)
            .Include(x => x.SalesOrderItems)
                .ThenInclude(x => x.ProductVariant)
                    .ThenInclude(x => x.RiceVariety)
            .Include(x => x.MillingOrders)
                .ThenInclude(x => x.Status)
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(x =>
                x.SOCode.ToLower().Contains(kw) ||
                x.Customer.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        return await query
            .AsSplitQuery()
            .OrderByDescending(x => x.CreatedDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountAsync(string? keyword)
    {
        var query = _context.SalesOrders.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(x =>
                x.SOCode.ToLower().Contains(kw) ||
                x.Customer.Name.ToLower().Contains(kw) ||
                (x.Note != null && x.Note.ToLower().Contains(kw)));
        }

        return await query.CountAsync();
    }
}

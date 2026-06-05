using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class OutboundOrderItemRepository : RepositoryBase<OutboundOrderItem, int>, IOutboundOrderItemRepository
{
    private readonly BackendContext _context;

    public OutboundOrderItemRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<OutboundOrderItem?> GetByIdForWeightAttachAsync(int id)
    {
        return await _context.OutboundOrderItems
            .Include(x => x.OutboundOrder)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}

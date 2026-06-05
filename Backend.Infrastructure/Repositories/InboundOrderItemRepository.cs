using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class InboundOrderItemRepository : RepositoryBase<InboundOrderItem, int>, IInboundOrderItemRepository
{
    private readonly BackendContext _context;

    public InboundOrderItemRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<InboundOrderItem?> GetByIdForWeightAttachAsync(int id)
    {
        return await _context.InboundOrderItems
            .Include(x => x.InboundOrder)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}

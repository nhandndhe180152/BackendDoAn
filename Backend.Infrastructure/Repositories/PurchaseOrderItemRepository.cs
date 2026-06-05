using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class PurchaseOrderItemRepository : RepositoryBase<PurchaseOrderItem, int>, IPurchaseOrderItemRepository
{
    private readonly BackendContext _context;

    public PurchaseOrderItemRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<PurchaseOrderItem?> GetByIdForWeightAttachAsync(int id)
    {
        return await _context.PurchaseOrderItems
            .Include(x => x.PurchaseOrder)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}

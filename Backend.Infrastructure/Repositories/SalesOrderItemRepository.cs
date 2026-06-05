using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class SalesOrderItemRepository : RepositoryBase<SalesOrderItem, int>, ISalesOrderItemRepository
{
    private readonly BackendContext _context;

    public SalesOrderItemRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<SalesOrderItem?> GetByIdForWeightAttachAsync(int id)
    {
        return await _context.SalesOrderItems
            .Include(x => x.SalesOrder)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}

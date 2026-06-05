using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class StockTakeItemRepository : RepositoryBase<StockTakeItem, int>, IStockTakeItemRepository
{
    private readonly BackendContext _context;

    public StockTakeItemRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<StockTakeItem?> GetByIdForWeightAttachAsync(int id)
    {
        return await _context.StockTakeItems
            .Include(x => x.StockTake)
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }
}

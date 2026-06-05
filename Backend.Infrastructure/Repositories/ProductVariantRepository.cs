using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class ProductVariantRepository : RepositoryBase<ProductVariant, int>, IProductVariantRepository
{
    private readonly BackendContext _context;

    public ProductVariantRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<ProductVariantAggregate>> GetPagedAsync(ProductVariantDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any())
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = _context.ProductVariants
            .Where(x => !x.IsDeleted)
            .Select(x => new ProductVariantAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                UnitOfMeasureId = x.UnitOfMeasureId,
                UnitOfMeasureName = x.UnitOfMeasure.Name,
                SKU = x.SKU,
                QRCode = x.QRCode,
                CostPrice = x.CostPrice,
                SalePrice = x.SalePrice,
                Weight = x.Weight,
                AttributeValues = x.AttributeValues,
                ImageId = x.ImageId,
                ImageFileKey = x.Image != null ? x.Image.FileKey : null,
                IsActive = x.IsActive,
                MinStockLevel = x.MinStockLevel,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                                     (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                                     EF.Functions.Collate(x.SKU, SQLParams.Latin_General).Contains(keyword) ||
                                     EF.Functions.Collate(x.ProductName, SQLParams.Latin_General).Contains(keyword));
        }

        if (parameters.ProductId.HasValue)
        {
            query = query.Where(x => x.ProductId == parameters.ProductId.Value);
        }

        query = orderAscendingDirection 
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) 
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<ProductVariantAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }

    public async Task<ProductVariant?> GetActiveByIdAsync(int id)
    {
        return await _context.ProductVariants
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.IsActive && x.Id == id);
    }
}

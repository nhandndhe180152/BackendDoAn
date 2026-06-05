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

public class ProductAttributeRepository : RepositoryBase<ProductAttribute, int>, IProductAttributeRepository
{
    private readonly BackendContext _context;

    public ProductAttributeRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<ProductAttributeAggregate>> GetPagedAsync(ProductAttributeDTParameters parameters)
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

        var query = _context.ProductAttributes
            .Where(x => !x.IsDeleted)
            .Select(x => new ProductAttributeAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                                     (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)));
        }

        query = orderAscendingDirection 
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) 
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<ProductAttributeAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

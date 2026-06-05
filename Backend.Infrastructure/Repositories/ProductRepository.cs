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

public class ProductRepository : RepositoryBase<Product, int>, IProductRepository
{
    private readonly BackendContext _context;

    public ProductRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<ProductAggregate>> GetPagedAsync(ProductDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Length > parameters.Order[0].Column)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = _context.Products
            .Where(x => !x.IsDeleted)
            .Select(x => new ProductAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                ProductCategoryId = x.ProductCategoryId,
                ProductCategoryName = x.ProductCategory.Name,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                                     (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                                     EF.Functions.Collate(x.ProductCategoryName, SQLParams.Latin_General).Contains(keyword));
        }

        if (parameters.CategoryIds != null && parameters.CategoryIds.Any())
        {
            query = query.Where(x => parameters.CategoryIds.Contains(x.ProductCategoryId));
        }

        var nameFilter = parameters.Columns?
            .FirstOrDefault(x => x.Data == "name")?
            .Search?.Value;

        var descriptionFilter = parameters.Columns?
            .FirstOrDefault(x => x.Data == "description")?
            .Search?.Value;

        var activeFilter = parameters.Columns?
            .FirstOrDefault(x => x.Data == "isActive")?
            .Search?.Value;

        var createdDateFilter = parameters.Columns?
            .FirstOrDefault(x => x.Data == "createdDate")?
            .Search?.Value;

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            query = query.Where(x =>
                EF.Functions.Collate(x.Name, SQLParams.Latin_General)
                    .Contains(nameFilter));
        }

        if (!string.IsNullOrWhiteSpace(descriptionFilter))
        {
            query = query.Where(x =>
                x.Description != null &&
                EF.Functions.Collate(x.Description, SQLParams.Latin_General)
                    .Contains(descriptionFilter));
        }

        if (!string.IsNullOrWhiteSpace(activeFilter) &&
            bool.TryParse(activeFilter, out var isActive))
        {
            query = query.Where(x => x.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(createdDateFilter))
        {
            var dates = createdDateFilter.Split('|');

            if (dates.Length > 0 &&
                DateTime.TryParse(dates[0], out var fromDate))
            {
                query = query.Where(x => x.CreatedDate.Date >= fromDate.Date);
            }

            if (dates.Length > 1 &&
                DateTime.TryParse(dates[1], out var toDate))
            {
                query = query.Where(x => x.CreatedDate.Date <= toDate.Date);
            }
        }

        query = orderAscendingDirection 
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) 
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<ProductAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

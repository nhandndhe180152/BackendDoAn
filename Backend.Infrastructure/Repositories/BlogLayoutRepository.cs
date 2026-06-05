using System;
using System.Globalization;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class BlogLayoutRepository : RepositoryBase<BlogPostLayout, int>, IBlogLayoutRepository
{
    private readonly BackendContext _context;
    public BlogLayoutRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<BlogLayoutAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "desc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = _context.BlogPostLayouts
            .Where(x => !x.IsDeleted)
            .Select(x => new BlogLayoutAggregate
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                 .Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                     (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                    x.CreatedDate.ToVietnameseDateTime().Contains(keyword)
                     );
        }
        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (!search.Contains("/"))
            {
                search = column.Search.Value.ToLower();
            }
            if (string.IsNullOrEmpty(search)) continue;
            switch (column.Data)
            {
                case "name":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Name, SQLParams.Latin_General).Contains(search));
                    break;
                case "description":
                    query = query.Where(r => r.Description != null && EF.Functions.Collate(r.Description, SQLParams.Latin_General).Contains(search));
                    break;
                case "createdDate":
                    if (search.Contains(" - "))
                    {
                        var dates = search.Split(" - ");
                        var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                        query = query.Where(c => c.CreatedDate >= startDate && c.CreatedDate <= endDate);
                    }
                    else
                    {
                        var date = DateTime.ParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        query = query.Where(c => c.CreatedDate.Date == date.Date);
                    }
                    break;
            }
        }
        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<BlogLayoutAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}
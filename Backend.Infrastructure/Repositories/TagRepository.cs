using System;
using System.Globalization;
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

public class TagRepository : RepositoryBase<Tag, int>, ITagRepository
{
    private readonly BackendContext _context;
    public TagRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<TagAggregate>> GetPagedAsync(TagDTParamters parameters)
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

        var query = _context.Tags
            .Where(x => !x.IsDeleted && !x.TagType.IsDeleted)
            .Select(x => new TagAggregate
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
                TagTypeId = x.TagTypeId,
                TagTypeName = x.TagType.Name
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                    (!parameters.IsIncludeTagType && x.TagTypeName != null && EF.Functions.Collate(x.TagTypeName, SQLParams.Latin_General).Contains(keyword)) ||
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
                case "tagTypeId":
                    query = query.Where(r => r.TagTypeId.ToString().ToLower().Contains(search));
                    break;
                case "tagTypeName":
                    query = query.Where(r => r.TagTypeName != null && EF.Functions.Collate(r.TagTypeName, SQLParams.Latin_General).Contains(search));
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

        if (parameters.TagTypeIds.Count > 0)
        {
            query = query.Where(x => parameters.TagTypeIds.Contains(x.TagTypeId));
        }

        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<TagAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

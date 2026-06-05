using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

public class WarehouseRepository : RepositoryBase<Warehouse, int>, IWarehouseRepository
{
    private readonly BackendContext _context;
    public WarehouseRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<WarehouseAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.Warehouses
            .Where(x => !x.IsDeleted)
            .Select(x => new WarehouseAggregate
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Address = x.Address,
                Description = x.Description,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query
                .Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.Code, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Address != null && EF.Functions.Collate(x.Address, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword))
                );
        }

        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (string.IsNullOrEmpty(search)) continue;
            search = search.ToLower();
            switch (column.Data)
            {
                case "name":
                    query = query.Where(r => EF.Functions.Collate(r.Name, SQLParams.Latin_General).Contains(search));
                    break;
                case "code":
                    query = query.Where(r => EF.Functions.Collate(r.Code, SQLParams.Latin_General).Contains(search));
                    break;
                case "address":
                    query = query.Where(r => r.Address != null && EF.Functions.Collate(r.Address, SQLParams.Latin_General).Contains(search));
                    break;
                case "isActive":
                    if (bool.TryParse(search, out var isActiveVal))
                    {
                        query = query.Where(r => r.IsActive == isActiveVal);
                    }
                    break;
            }
        }

        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<WarehouseAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

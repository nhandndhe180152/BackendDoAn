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

public class LocationRepository : RepositoryBase<Location, int>, ILocationRepository
{
    private readonly BackendContext _context;
    public LocationRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<LocationAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.Locations
            .Where(x => !x.IsDeleted)
            .Select(x => new LocationAggregate
            {
                Id = x.Id,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                ZoneName = x.ZoneName,
                ShelfRow = x.ShelfRow,
                ShelfLevel = x.ShelfLevel,
                SlotCode = x.SlotCode,
                MaxCapacity = x.MaxCapacity,
                Description = x.Description,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query
                .Where(x => EF.Functions.Collate(x.ZoneName, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.WarehouseName, SQLParams.Latin_General).Contains(keyword) ||
                    (x.ShelfRow != null && EF.Functions.Collate(x.ShelfRow, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.ShelfLevel != null && EF.Functions.Collate(x.ShelfLevel, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.SlotCode != null && EF.Functions.Collate(x.SlotCode, SQLParams.Latin_General).Contains(keyword))
                );
        }

        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (string.IsNullOrEmpty(search)) continue;
            search = search.ToLower();
            switch (column.Data)
            {
                case "zoneName":
                    query = query.Where(r => EF.Functions.Collate(r.ZoneName, SQLParams.Latin_General).Contains(search));
                    break;
                case "warehouseId":
                    if (int.TryParse(search, out var whId))
                    {
                        query = query.Where(r => r.WarehouseId == whId);
                    }
                    break;
                case "slotCode":
                    query = query.Where(r => r.SlotCode != null && EF.Functions.Collate(r.SlotCode, SQLParams.Latin_General).Contains(search));
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

        var data = new DTResult<LocationAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

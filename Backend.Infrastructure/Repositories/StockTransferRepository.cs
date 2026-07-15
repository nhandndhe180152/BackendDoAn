using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class StockTransferRepository : RepositoryBase<StockTransfer, int>, IStockTransferRepository
{
    private readonly BackendContext _context;

    public StockTransferRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<StockTransferAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "Id";
        var orderAscendingDirection = false;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.StockTransfers
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new StockTransferAggregate
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                TransferCode = x.TransferCode,
                StatusId = x.StatusId,
                StatusName = x.Status.Name,
                FromWarehouseId = x.FromWarehouseId,
                FromWarehouseName = x.FromWarehouse.Name,
                ToWarehouseId = x.ToWarehouseId,
                ToWarehouseName = x.ToWarehouse.Name,
                AssignedUserId = x.AssignedUserId,
                TransferDate = x.TransferDate,
                Note = x.Note,
                ItemCount = x.StockTransferItems.Count,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.TransferCode.Contains(keyword) ||
                (x.FromWarehouseName != null && x.FromWarehouseName.Contains(keyword)) ||
                (x.ToWarehouseName != null && x.ToWarehouseName.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;
                switch (column.Data)
                {
                    case "statusId":
                    case "StatusId":
                        if (int.TryParse(search, out var sId))
                            query = query.Where(x => x.StatusId == sId);
                        break;
                    case "fromWarehouseId":
                    case "FromWarehouseId":
                        if (int.TryParse(search, out var fwId))
                            query = query.Where(x => x.FromWarehouseId == fwId);
                        break;
                    case "toWarehouseId":
                    case "ToWarehouseId":
                        if (int.TryParse(search, out var twId))
                            query = query.Where(x => x.ToWarehouseId == twId);
                        break;
                    case "transferDate":
                    case "TransferDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var start = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var end = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.TransferDate >= start && x.TransferDate <= end);
                        }
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync();

        return new DTResult<StockTransferAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "transferCode" => "TransferCode",
        "statusId" => "StatusId",
        "transferDate" => "TransferDate",
        "createdDate" => "CreatedDate",
        _ => "Id"
    };
}

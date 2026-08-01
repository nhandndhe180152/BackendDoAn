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

        var entityQuery = _context.StockTransfers
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var totalRecord = await entityQuery.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            entityQuery = entityQuery.Where(x =>
                x.TransferCode.Contains(keyword) ||
                x.FromWarehouse.Name.Contains(keyword) ||
                x.ToWarehouse.Name.Contains(keyword) ||
                x.StockTransferItems.Any(i =>
                    !i.IsDeleted &&
                    (i.ProductVariant.SKU.Contains(keyword) ||
                     i.ProductVariant.Name.Contains(keyword) ||
                     (i.PaddyLot != null && i.PaddyLot.LotCode.Contains(keyword)))));
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
                            entityQuery = entityQuery.Where(x => x.StatusId == sId);
                        break;
                    case "statusName":
                    case "StatusName":
                        entityQuery = entityQuery.Where(x => x.Status.Name == search);
                        break;
                    case "fromWarehouseId":
                    case "FromWarehouseId":
                        if (int.TryParse(search, out var fwId))
                            entityQuery = entityQuery.Where(x => x.FromWarehouseId == fwId);
                        break;
                    case "toWarehouseId":
                    case "ToWarehouseId":
                        if (int.TryParse(search, out var twId))
                            entityQuery = entityQuery.Where(x => x.ToWarehouseId == twId);
                        break;
                    case "transferDate":
                    case "TransferDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            if (DateTime.TryParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
                                DateTime.TryParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
                            {
                                var end = endDate.AddDays(1).AddTicks(-1);
                                entityQuery = entityQuery.Where(x => x.TransferDate >= start && x.TransferDate <= end);
                            }
                        }
                        else if (DateTime.TryParseExact(
                            search,
                            "dd/MM/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var exactDate))
                        {
                            var nextDate = exactDate.AddDays(1);
                            entityQuery = entityQuery.Where(x =>
                                x.TransferDate >= exactDate &&
                                x.TransferDate < nextDate);
                        }
                        break;
                }
            }
        }

        var filteredRecord = await entityQuery.CountAsync();

        var query = entityQuery.Select(x => new StockTransferAggregate
        {
            Id = x.Id,
            OrganizationId = x.OrganizationId,
            TransferCode = x.TransferCode,
            StatusId = x.StatusId,
            StatusName = x.Status.Name,
            StatusColor = x.Status.Color,
            FromWarehouseId = x.FromWarehouseId,
            FromWarehouseName = x.FromWarehouse.Name,
            ToWarehouseId = x.ToWarehouseId,
            ToWarehouseName = x.ToWarehouse.Name,
            AssignedUserId = x.AssignedUserId,
            TransferDate = x.TransferDate,
            Note = x.Note,
            ItemCount = x.StockTransferItems.Count(i => !i.IsDeleted),
            TotalWeightKg = x.StockTransferItems
                .Where(i => !i.IsDeleted)
                .Sum(i => (decimal?)i.WeightKg) ?? 0m,
            ItemDisplay = x.StockTransferItems
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.Id)
                .Select(i => i.PaddyLot != null && i.PaddyLot.LotCode != null
                    ? i.PaddyLot.LotCode
                    : i.ProductVariant.SKU)
                .FirstOrDefault(),
            CreatedDate = x.CreatedDate
        });

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

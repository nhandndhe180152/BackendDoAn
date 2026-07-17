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

public class AlertRepository : RepositoryBase<Alert, int>, IAlertRepository
{
    private readonly BackendContext _context;

    public AlertRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<AlertAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.Alerts
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new AlertAggregate
            {
                Id = x.Id,
                AlertType = x.AlertType,
                Severity = x.Severity,
                WarehouseId = x.WarehouseId,
                WarehouseCode = x.Warehouse != null ? x.Warehouse.Code : null,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : null,
                ProductVariantId = x.ProductVariantId,
                ProductVariantSku = x.ProductVariant != null ? x.ProductVariant.SKU : null,
                ProductName = x.ProductVariant != null && x.ProductVariant.Product != null ? x.ProductVariant.Product.Name : null,
                LocationId = x.LocationId,
                LocationName = x.Location != null ? x.Location.ZoneName : null,
                Message = x.Message,
                RelatedEntityType = x.RelatedEntityType,
                RelatedEntityId = x.RelatedEntityId,
                Status = x.Status,
                AcknowledgedBy = x.AcknowledgedBy,
                AcknowledgedByName = x.AcknowledgedByUser != null
                    ? x.AcknowledgedByUser.FirstName + " " + x.AcknowledgedByUser.LastName
                    : null,
                AcknowledgedAt = x.AcknowledgedAt,
                ResolvedAt = x.ResolvedAt,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Message.Contains(keyword) ||
                x.AlertType.Contains(keyword) ||
                (x.WarehouseName != null && x.WarehouseName.Contains(keyword)) ||
                (x.ProductVariantSku != null && x.ProductVariantSku.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "alertType":
                    case "AlertType":
                        query = query.Where(x => x.AlertType == search);
                        break;
                    case "severity":
                    case "Severity":
                        query = query.Where(x => x.Severity == search);
                        break;
                    case "status":
                    case "Status":
                        query = query.Where(x => x.Status == search);
                        break;
                    case "warehouseId":
                    case "WarehouseId":
                        if (int.TryParse(search, out var warehouseId))
                            query = query.Where(x => x.WarehouseId == warehouseId);
                        break;
                    case "productVariantId":
                    case "ProductVariantId":
                        if (int.TryParse(search, out var variantId))
                            query = query.Where(x => x.ProductVariantId == variantId);
                        break;
                    case "createdDate":
                    case "CreatedDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.CreatedDate >= startDate && x.CreatedDate <= endDate);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        {
                            query = query.Where(x => x.CreatedDate.Date == date.Date);
                        }
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return new DTResult<AlertAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName)
    {
        return columnName switch
        {
            "id" => "Id",
            "alertType" => "AlertType",
            "severity" => "Severity",
            "status" => "Status",
            "warehouseId" => "WarehouseId",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}

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

public class PaddyLotRepository : RepositoryBase<PaddyLot, int>, IPaddyLotRepository
{
    private readonly BackendContext _context;

    public PaddyLotRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<PaddyLotAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.PaddyLots
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new PaddyLotAggregate
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                LotCode = x.LotCode,
                LotType = x.LotType,
                ProductVariantId = x.ProductVariantId,
                SKU = x.ProductVariant.SKU,
                ProductVariantName = x.ProductVariant.Name,
                RiceVarietyId = x.RiceVarietyId,
                RiceVarietyName = x.RiceVariety == null ? null : x.RiceVariety.Name,
                StatusId = x.StatusId,
                StatusName = x.Status.Name,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                LocationId = x.LocationId,
                InitialWeightKg = x.InitialWeightKg,
                RemainingWeightKg = x.RemainingWeightKg,
                CostPricePerKg = x.CostPricePerKg,
                QualityStatus = x.QualityStatus,
                InboundDate = x.InboundDate,
                SourceReceiptId = x.SourceReceiptId,
                SourceMillingOrderId = x.SourceMillingOrderId,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.LotCode.Contains(keyword) ||
                (x.SKU != null && x.SKU.Contains(keyword)) ||
                (x.ProductVariantName != null && x.ProductVariantName.Contains(keyword)) ||
                (x.RiceVarietyName != null && x.RiceVarietyName.Contains(keyword)) ||
                (x.WarehouseName != null && x.WarehouseName.Contains(keyword)) ||
                (x.QualityStatus != null && x.QualityStatus.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "lotType":
                    case "LotType":
                        query = query.Where(x => x.LotType.Contains(search));
                        break;
                    case "warehouseId":
                    case "WarehouseId":
                        if (int.TryParse(search, out var wId))
                            query = query.Where(x => x.WarehouseId == wId);
                        break;
                    case "statusId":
                    case "StatusId":
                        if (int.TryParse(search, out var sId))
                            query = query.Where(x => x.StatusId == sId);
                        break;
                    case "riceVarietyId":
                    case "RiceVarietyId":
                        if (int.TryParse(search, out var rvId))
                            query = query.Where(x => x.RiceVarietyId == rvId);
                        break;
                    case "inboundDate":
                    case "InboundDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var start = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var end = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.InboundDate >= start && x.InboundDate <= end);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        {
                            query = query.Where(x => x.InboundDate.Date == d.Date);
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

        return new DTResult<PaddyLotAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "id" => "Id",
        "lotCode" => "LotCode",
        "lotType" => "LotType",
        "statusId" => "StatusId",
        "warehouseId" => "WarehouseId",
        "inboundDate" => "InboundDate",
        "remainingWeightKg" => "RemainingWeightKg",
        "costPricePerKg" => "CostPricePerKg",
        "createdDate" => "CreatedDate",
        _ => "Id"
    };
}

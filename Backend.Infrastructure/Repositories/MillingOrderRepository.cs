using System;
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

public class MillingOrderRepository : RepositoryBase<MillingOrder, int>, IMillingOrderRepository
{
    private readonly BackendContext _context;

    public MillingOrderRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<MillingOrderAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.MillingOrders
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new MillingOrderAggregate
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                MillingCode = x.MillingCode,
                StatusId = x.StatusId,
                StatusName = x.Status.Name,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                Reason = x.Reason,
                SalesOrderId = x.SalesOrderId,
                YieldRateUsed = x.YieldRateUsed,
                TotalRiceOutputKg = x.TotalRiceOutputKg,
                ComputedPaddyKg = x.ComputedPaddyKg,
                ByproductKg = x.ByproductKg,
                LossKg = x.LossKg,
                MachineRef = x.MachineRef,
                OperatorId = x.OperatorId,
                StartedAt = x.StartedAt,
                CompletedAt = x.CompletedAt,
                TotalCost = x.TotalCost,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.MillingCode.Contains(keyword) ||
                (x.WarehouseName != null && x.WarehouseName.Contains(keyword)) ||
                (x.MachineRef != null && x.MachineRef.Contains(keyword)));
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
                    case "warehouseId":
                    case "WarehouseId":
                        if (int.TryParse(search, out var wId))
                            query = query.Where(x => x.WarehouseId == wId);
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

        return new DTResult<MillingOrderAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "millingCode" => "MillingCode",
        "statusId" => "StatusId",
        "totalRiceOutputKg" => "TotalRiceOutputKg",
        "startedAt" => "StartedAt",
        "completedAt" => "CompletedAt",
        "createdDate" => "CreatedDate",
        _ => "Id"
    };
}

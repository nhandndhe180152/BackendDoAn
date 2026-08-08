using System;
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

public class StockTakeRepository : RepositoryBase<StockTake, int>, IStockTakeRepository
{
    private readonly BackendContext _context;
    public StockTakeRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<StockTakeAggregate>> GetPagedAsync(DTParameter parameters)
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
            orderAscendingDirection = false;
        }

        var query = _context.StockTakes
            .Where(x => !x.IsDeleted)
            .Select(x => new StockTakeAggregate
            {
                Id = x.Id,
                STCode = x.STCode,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                StockTakeStatusId = x.StockTakeStatusId,
                StockTakeStatusCode = x.StockTakeStatus.Code,
                StockTakeStatusName = x.StockTakeStatus.Name,
                StockTakeStatusColor = x.StockTakeStatus.Color,
                Note = x.Note,
                StartedDate = x.StartedDate,
                CompletedDate = x.CompletedDate,
                ApprovedByUserId = x.ApprovedByUserId,
                ApproveNote = x.ApproveNote,
                CreatedDate = x.CreatedDate,
                CreatedByUserId = x.CreatedBy,
                CreatedByName = _context.Users
                    .Where(u => u.Id == x.CreatedBy && !u.IsDeleted)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
                ScopeDisplay = x.StockTakeItems
                    .Where(i => !i.IsDeleted)
                    .OrderBy(i => i.Id)
                    .Select(i => i.PaddyLot != null
                        ? "Lô " + i.PaddyLot.LotCode
                        : i.Location != null
                            ? i.Location.ZoneName + " / " + (i.Location.SlotCode ?? i.Location.ShelfRow ?? "Vị trí")
                            : i.ProductVariant != null
                                ? "SKU " + i.ProductVariant.SKU
                                : "Toàn kho")
                    .FirstOrDefault() ?? "Chưa có dòng",
                ItemCount = x.StockTakeItems.Count(i => !i.IsDeleted),
                VarianceLineCount = x.StockTakeItems.Count(i =>
                    !i.IsDeleted && i.ActualQuantity.HasValue &&
                    i.ActualQuantity.Value != i.SystemQuantity),
                NetVarianceKg = x.StockTakeItems
                    .Where(i => !i.IsDeleted && i.ActualQuantity.HasValue)
                    .Sum(i => (decimal?)(i.ActualQuantity!.Value - i.SystemQuantity)) ?? 0m
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query
                .Where(x => EF.Functions.Collate(x.STCode, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Note != null && EF.Functions.Collate(x.Note, SQLParams.Latin_General).Contains(keyword)) ||
                    EF.Functions.Collate(x.WarehouseName, SQLParams.Latin_General).Contains(keyword)
                );
        }

        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (string.IsNullOrEmpty(search)) continue;
            search = search.ToLower();
            switch (column.Data)
            {
                case "sTCode":
                    query = query.Where(r => EF.Functions.Collate(r.STCode, SQLParams.Latin_General).Contains(search));
                    break;
                case "warehouseId":
                    if (int.TryParse(search, out var wId))
                    {
                        query = query.Where(r => r.WarehouseId == wId);
                    }
                    break;
                case "stockTakeStatusId":
                    if (int.TryParse(search, out var statusId))
                    {
                        query = query.Where(r => r.StockTakeStatusId == statusId);
                    }
                    break;
            }
        }

        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<StockTakeAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

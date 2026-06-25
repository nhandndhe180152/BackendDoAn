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

public class UnitOfMeasureRepository : RepositoryBase<UnitOfMeasure, int>, IUnitOfMeasureRepository
{
    private readonly BackendContext _context;

    public UnitOfMeasureRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<UnitOfMeasureAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "Id";
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.UnitOfMeasures
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new UnitOfMeasureAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Symbol = x.Symbol,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.Symbol.Contains(keyword));
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return new DTResult<UnitOfMeasureAggregate>
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
            "name" => "Name",
            "symbol" => "Symbol",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}

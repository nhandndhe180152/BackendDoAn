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

public class RiceVarietyRepository : RepositoryBase<RiceVariety, int>, IRiceVarietyRepository
{
    private readonly BackendContext _context;

    public RiceVarietyRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<RiceVarietyAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.RiceVarieties
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new RiceVarietyAggregate
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                Code = x.Code,
                Name = x.Name,
                Season = x.Season,
                DefaultYieldRate = x.DefaultYieldRate,
                Note = x.Note,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.Code.Contains(keyword) ||
                (x.Season != null && x.Season.Contains(keyword)) ||
                (x.Note != null && x.Note.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "name":
                    case "Name":
                        query = query.Where(x => x.Name.Contains(search));
                        break;
                    case "code":
                    case "Code":
                        query = query.Where(x => x.Code.Contains(search));
                        break;
                    case "season":
                    case "Season":
                        query = query.Where(x => x.Season != null && x.Season.Contains(search));
                        break;
                    case "isActive":
                    case "IsActive":
                        if (bool.TryParse(search, out var isActive))
                            query = query.Where(x => x.IsActive == isActive);
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

        return new DTResult<RiceVarietyAggregate>
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
            "code" => "Code",
            "season" => "Season",
            "defaultYieldRate" => "DefaultYieldRate",
            "isActive" => "IsActive",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}

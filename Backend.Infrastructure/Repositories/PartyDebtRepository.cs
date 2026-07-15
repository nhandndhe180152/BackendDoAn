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

public class PartyDebtRepository : RepositoryBase<PartyDebt, int>, IPartyDebtRepository
{
    private readonly BackendContext _context;

    public PartyDebtRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<PartyDebtAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.PartyDebts
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new PartyDebtAggregate
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                PartyType = x.PartyType,
                PartyId = x.PartyId,
                Direction = x.Direction,
                OpeningBalance = x.OpeningBalance,
                CurrentBalance = x.CurrentBalance,
                CreditLimit = x.CreditLimit,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
                LastModifiedDate = x.LastModifiedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.PartyType.Contains(keyword) ||
                x.Direction.Contains(keyword));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "partyType":
                    case "PartyType":
                        query = query.Where(x => x.PartyType == search.ToUpper());
                        break;
                    case "direction":
                    case "Direction":
                        query = query.Where(x => x.Direction == search.ToUpper());
                        break;
                    case "partyId":
                    case "PartyId":
                        if (int.TryParse(search, out var pId))
                            query = query.Where(x => x.PartyId == pId);
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

        return new DTResult<PartyDebtAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "partyType" => "PartyType",
        "direction" => "Direction",
        "currentBalance" => "CurrentBalance",
        "createdDate" => "CreatedDate",
        _ => "Id"
    };
}

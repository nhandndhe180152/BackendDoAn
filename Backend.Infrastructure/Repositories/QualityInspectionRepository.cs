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

public class QualityInspectionRepository : RepositoryBase<QualityInspection, int>, IQualityInspectionRepository
{
    private readonly BackendContext _context;

    public QualityInspectionRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<QualityInspectionAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "InspectedAt";
        var orderAscendingDirection = false;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.QualityInspections
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new QualityInspectionAggregate
            {
                Id = x.Id,
                PaddyLotId = x.PaddyLotId,
                LotCode = x.PaddyLot.LotCode,
                InspectorId = x.InspectorId,
                InspectorName = x.Inspector == null ? null : (x.Inspector.FirstName + " " + x.Inspector.LastName).Trim(),
                InspectedAt = x.InspectedAt,
                MoisturePercent = x.MoisturePercent,
                ImpurityPercent = x.ImpurityPercent,
                MoldLevel = x.MoldLevel,
                PestLevel = x.PestLevel,
                PackagingStatus = x.PackagingStatus,
                PassedInspection = x.PassedInspection,
                Handling = x.Handling,
                Note = x.Note,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                (x.LotCode != null && x.LotCode.Contains(keyword)) ||
                (x.InspectorName != null && x.InspectorName.Contains(keyword)) ||
                (x.Handling != null && x.Handling.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;
                switch (column.Data)
                {
                    case "paddyLotId":
                    case "PaddyLotId":
                        if (int.TryParse(search, out var lId))
                            query = query.Where(x => x.PaddyLotId == lId);
                        break;
                    case "passedInspection":
                    case "PassedInspection":
                        if (bool.TryParse(search, out var passed))
                            query = query.Where(x => x.PassedInspection == passed);
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync();

        return new DTResult<QualityInspectionAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "inspectedAt" => "InspectedAt",
        "paddyLotId" => "PaddyLotId",
        "passedInspection" => "PassedInspection",
        "createdDate" => "CreatedDate",
        _ => "InspectedAt"
    };
}

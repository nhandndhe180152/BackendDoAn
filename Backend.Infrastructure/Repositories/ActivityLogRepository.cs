using System;
using System.Globalization;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class ActivityLogRepository : RepositoryBase<ActivityLog, int>, IActivityLogRepository
{
    private readonly BackendContext _context;
    public ActivityLogRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<ActivityLogAggregate>> GetPagedAsync(ActivityLogDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null && parameters.Order.Length > 0)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = from a in _context.ActivityLogs
                    join b in _context.Users on a.CreatedBy equals b.Id into groupAB
                    from c in groupAB.DefaultIfEmpty()
                    where !a.IsDeleted && !c.IsDeleted
                    select new ActivityLogAggregate
                    {
                        Id = a.Id,
                        Action = a.Action,
                        CreatedDate = a.CreatedDate,
                        Description = a.Description,
                        IpAddress = a.IpAddress,
                        UserAgent = a.UserAgent,
                        CreatedUserName = c == null ? null : c.FirstName + " " + c.LastName,
                        CreatedUserId = a.CreatedBy
                    };
        if (parameters.UserId > 0)
        {
            query = query.Where(x => x.CreatedUserId == parameters.UserId);
        }
        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => EF.Functions.Collate(x.Action, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.IpAddress != null && EF.Functions.Collate(x.IpAddress, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.UserAgent != null && EF.Functions.Collate(x.UserAgent, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.CreatedUserName != null && EF.Functions.Collate(x.CreatedUserName, SQLParams.Latin_General).Contains(keyword)) ||
                    x.CreatedDate.ToVietnameseDateTime().Contains(keyword)
                 );
        }
        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (!search.Contains("/"))
            {
                search = column.Search.Value.ToLower();
            }
            if (string.IsNullOrEmpty(search)) continue;
            switch (column.Data)
            {
                case "action":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Action, SQLParams.Latin_General).Contains(search));
                    break;
                case "ipAddress":
                    query = query
                        .Where(r => r.IpAddress != null && EF.Functions.Collate(r.IpAddress, SQLParams.Latin_General).Contains(search));
                    break;
                case "description":
                    query = query.Where(r => r.Description != null && EF.Functions.Collate(r.Description, SQLParams.Latin_General).Contains(search));
                    break;
                case "userAgent":
                    query = query.Where(r => r.UserAgent != null && EF.Functions.Collate(r.UserAgent, SQLParams.Latin_General).Contains(search));
                    break;
                case "createdDate":
                    if (search.Contains(" - "))
                    {
                        var dates = search.Split(" - ");
                        var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                        query = query.Where(c => c.CreatedDate >= startDate && c.CreatedDate <= endDate);
                    }
                    else
                    {
                        var date = DateTime.ParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        query = query.Where(c => c.CreatedDate.Date == date.Date);
                    }
                    break;
            }
        }
        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<ActivityLogAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}

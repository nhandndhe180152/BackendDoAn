using System;
using System.Globalization;
using Backend.Application.Constants;
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

public class AuditLogRepository : RepositoryBase<AuditLog, int>, IAuditLogRepository
{
    private readonly BackendContext _context;
    public AuditLogRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }
    public async Task<DTResult<AuditLogAggregate>> GetPagedAsync(AuditLogDTParameters parameters)
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
            orderAscendingDirection = false;
        }

        var isGetAll = parameters.RoleIds.Any(x => x == CommonConstants.Role.ADMIN || x == CommonConstants.Role.EXECUTIVE);

        var query = from a in _context.AuditLogs
                    join b in _context.Users on a.CreatedBy equals b.Id into groupAB
                    from c in groupAB.DefaultIfEmpty()
                    where !a.IsDeleted && (c == null || !c.IsDeleted)
                    && (isGetAll || a.CreatedBy == parameters.UserId)
                    select new AuditLogAggregate
                    {
                        Id = a.Id,
                        Action = a.Action,
                        TargetType = a.TargetType,
                        TargetId = a.TargetId,
                        //DataBefore = a.DataBefore,
                        //DataAfter = a.DataAfter,
                        Description = a.Description,
                        IpAddress = a.IpAddress,
                        UserAgent = a.UserAgent,
                        CreatedDate = a.CreatedDate,
                        CreatedUserName = c == null ? null : c.FirstName + " " + c.LastName,
                        CreatedUserId = a.CreatedBy
                    };

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x =>
                EF.Functions.Collate(x.Action, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.TargetType, SQLParams.Latin_General).Contains(keyword) ||
                (x.TargetId != null && EF.Functions.Collate(x.TargetId, SQLParams.Latin_General).Contains(keyword)) ||
                (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                (x.IpAddress != null && EF.Functions.Collate(x.IpAddress, SQLParams.Latin_General).Contains(keyword)) ||
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
                    query = query.Where(r => EF.Functions.Collate(r.Action, SQLParams.Latin_General).Contains(search));
                    break;
                case "targetType":
                    query = query.Where(r => EF.Functions.Collate(r.TargetType, SQLParams.Latin_General).Contains(search));
                    break;
                case "targetId":
                    query = query.Where(r => r.TargetId != null && EF.Functions.Collate(r.TargetId, SQLParams.Latin_General).Contains(search));
                    break;
                case "description":
                    query = query.Where(r => r.Description != null && EF.Functions.Collate(r.Description, SQLParams.Latin_General).Contains(search));
                    break;
                case "ipAddress":
                    query = query.Where(r => r.IpAddress != null && EF.Functions.Collate(r.IpAddress, SQLParams.Latin_General).Contains(search));
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

        if (parameters.Actions.Any())
        {
            query = query
                .Where(x => parameters.Actions.Contains(x.Action));
        }

        if (parameters.TargetTypes.Any())
        {
            query = query
                .Where(x => parameters.TargetTypes.Contains(x.TargetType));
        }

        var recordsFiltered = await query.CountAsync();

        query = orderAscendingDirection ?
            query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) :
            query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var records = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync();
        records.ForEach(item =>
        {
            item.ActionName = CommonConstants.ListAction.GetValueOrDefault(item.Action) ?? string.Empty;
            item.TargetTypeName = CommonConstants.EntityDisplayMap.GetValueOrDefault(item.TargetType) ?? item.TargetType;
        });

        var data = new DTResult<AuditLogAggregate>
        {
            draw = parameters.Draw,
            data = records,
            recordsFiltered = recordsFiltered,
            recordsTotal = totalRecord
        };

        return data;
    }
}

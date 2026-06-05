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

public class NotificationRepository : RepositoryBase<Notification, int>, INotificationRepository
{
    private readonly BackendContext _context;
    public NotificationRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;

    }
    public async Task<DTResult<NotificationAggregate>> GetPagedAsync(NotificationDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null && parameters.Order.Length > 0)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() != "desc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = Enumerable.Empty<NotificationAggregate>().AsQueryable();

        if (parameters.IsAdmin)
        {
            query = from no in _context.Notifications
                    join noc in _context.NotificationCategories on no.NotificationCategoryId equals noc.Id
                    where !no.IsDeleted && !noc.IsDeleted
                    select new NotificationAggregate
                    {
                        Id = no.Id,
                        NotificationCategoryId = no.NotificationCategoryId,
                        NotificationCategoryName = noc.Name,
                        NotificationCategoryColor = noc.Color,
                        DirectionId = no.DirectionId,
                        Title = no.Title,
                        Content = no.Content,
                        CreatedDate = no.CreatedDate,
                        //Users = (from uno in no.UserNotifications
                        //         join usr in _context.Users on uno.UserId equals usr.Id
                        //         where !uno.IsDeleted && !usr.IsDeleted
                        //         select new UserDetailAggregate()
                        //         {
                        //             Id = usr.Id,
                        //             Name = usr.FirstName + " " + usr.LastName,
                        //             Username = usr.Username,
                        //         }).ToList()
                    };
        }
        else
        {
            query = from a in _context.UserNotifications
                    join b in _context.Notifications on a.NotificationId equals b.Id
                    join c in _context.NotificationCategories on b.NotificationCategoryId equals c.Id
                    where !a.IsDeleted && !b.IsDeleted && !c.IsDeleted && a.UserId == parameters.UserId
                    select new NotificationAggregate
                    {
                        Id = b.Id,
                        NotificationCategoryId = b.NotificationCategoryId,
                        NotificationCategoryName = c.Name,
                        NotificationCategoryColor = c.Color,
                        DirectionId = b.DirectionId,
                        Title = b.Title,
                        Content = b.Content,
                        CreatedDate = b.CreatedDate,
                    };
        }

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => EF.Functions.Collate(x.Title, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.Content, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.NotificationCategoryName, SQLParams.Latin_General).Contains(keyword) ||
                    (x.DirectionId != null && EF.Functions.Collate(x.DirectionId, SQLParams.Latin_General).Contains(keyword)) ||
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
                case "title":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Title, SQLParams.Latin_General).Contains(search));
                    break;
                case "content":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Content, SQLParams.Latin_General).Contains(search));
                    break;
                case "direction":
                    query = query
                        .Where(r => r.DirectionId != null && EF.Functions.Collate(r.DirectionId, SQLParams.Latin_General).Contains(search));
                    break;
                case "notificationCategoryName":
                    query = query
                        .Where(r => EF.Functions.Collate(r.NotificationCategoryName, SQLParams.Latin_General).Contains(search));
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
        if (parameters.NotificationCategoryIds.Count > 0)
        {
            query = query.Where(x => parameters.NotificationCategoryIds.Contains(x.NotificationCategoryId));
        }
        //if (parameters.UserIds.Count > 0)
        //{
        //    query = query.Where(x => x.Users.Any(u => parameters.UserIds.Contains(u.Id)));
        //}
        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var result = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync();
        var data = new DTResult<NotificationAggregate>
        {
            draw = parameters.Draw,
            data = result,
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }

}

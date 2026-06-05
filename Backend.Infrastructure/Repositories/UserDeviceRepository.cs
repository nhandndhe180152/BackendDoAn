using System;
using System.Globalization;
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

public class UserDeviceRepository : RepositoryBase<UserDevice, int>, IUserDeviceRepository
{
    private readonly BackendContext _context;
    public UserDeviceRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<UserDeviceAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "desc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = from a in _context.UserDevices
                    join b in _context.Users on a.UserId equals b.Id
                    where !a.IsDeleted && !b.IsDeleted
                    select new UserDeviceAggregate
                    {
                        Id = a.Id,
                        AppVersion = a.AppVersion,
                        DeviceName = a.DeviceName,
                        DeviceToken = a.DeviceToken,
                        OsVersion = a.OsVersion,
                        Platform = a.Platform,
                        UserAgent = a.UserAgent,
                        CreatedDate = a.CreatedDate,
                        UserId = a.UserId,
                        UserName = b.FirstName + b.LastName
                    };

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => (x.AppVersion != null && EF.Functions.Collate(x.AppVersion, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.DeviceName != null && EF.Functions.Collate(x.DeviceName, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.DeviceToken != null && EF.Functions.Collate(x.DeviceToken, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.OsVersion != null && EF.Functions.Collate(x.OsVersion, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.Platform != null && EF.Functions.Collate(x.Platform, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.UserAgent != null && EF.Functions.Collate(x.UserAgent, SQLParams.Latin_General).Contains(keyword)) ||
                    EF.Functions.Collate(x.UserName, SQLParams.Latin_General).Contains(keyword) ||
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
                case "appVersion":
                    query = query
                        .Where(r => r.AppVersion != null && EF.Functions.Collate(r.AppVersion, SQLParams.Latin_General).Contains(search));
                    break;
                case "deviceName":
                    query = query
                        .Where(r => r.DeviceName != null && EF.Functions.Collate(r.DeviceName, SQLParams.Latin_General).Contains(search));
                    break;
                case "deviceToken":
                    query = query
                        .Where(r => r.DeviceToken != null && EF.Functions.Collate(r.DeviceToken, SQLParams.Latin_General).Contains(search));
                    break;
                case "osVersion":
                    query = query
                        .Where(r => r.OsVersion != null && EF.Functions.Collate(r.OsVersion, SQLParams.Latin_General).Contains(search));
                    break;
                case "platform":
                    query = query
                        .Where(r => r.Platform != null && EF.Functions.Collate(r.Platform, SQLParams.Latin_General).Contains(search));
                    break;
                case "userAgent":
                    query = query
                        .Where(r => r.UserAgent != null && EF.Functions.Collate(r.UserAgent, SQLParams.Latin_General).Contains(search));
                    break;
                case "userName":
                    query = query.Where(r => EF.Functions.Collate(r.UserName, SQLParams.Latin_General).Contains(search));
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

        var data = new DTResult<UserDeviceAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }

    public async Task MarkTokensAsDeletedAsync(List<string> deviceTokens)
    {
        if (deviceTokens == null || deviceTokens.Count == 0)
            return;

        var devices = await _context.UserDevices
            .Where(x => !string.IsNullOrEmpty(x.DeviceToken) && deviceTokens.Contains(x.DeviceToken) && !x.IsDeleted)
            .ToListAsync();

        if (devices.Count == 0)
            return;

        foreach (var device in devices)
        {
            device.IsDeleted = true;
        }

        await _context.SaveChangesAsync();
    }

}

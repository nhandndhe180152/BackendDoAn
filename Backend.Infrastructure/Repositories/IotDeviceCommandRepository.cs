using System;
using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class IotDeviceCommandRepository : RepositoryBase<IotDeviceCommand, int>, IIotDeviceCommandRepository
{
    private readonly BackendContext _context;

    public IotDeviceCommandRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    /// <summary>
    /// Thực hiện truy vấn hoặc cập nhật dữ liệu ở tầng Infrastructure bằng EF Core.
    /// </summary>
    /// <param name="commandId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="trackChanges">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotDeviceCommand?> GetCommandWithDeviceAsync(int commandId, bool trackChanges = true)
    {
        var query = trackChanges
            ? _context.IotDeviceCommands.Where(x => !x.IsDeleted)
            : _context.IotDeviceCommands.AsNoTracking().Where(x => !x.IsDeleted);

        return await query
            .Include(x => x.IotDevice)
            .FirstOrDefaultAsync(x => x.Id == commandId);
    }

    /// <summary>
    /// Thực hiện truy vấn hoặc cập nhật dữ liệu ở tầng Infrastructure bằng EF Core.
    /// </summary>
    /// <param name="iotDeviceId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="trackChanges">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotDeviceCommand?> GetNextPendingCommandAsync(int iotDeviceId, bool trackChanges = true)
    {
        var now = DateTime.Now;

        var query = trackChanges
            ? _context.IotDeviceCommands.Where(x => !x.IsDeleted)
            : _context.IotDeviceCommands.AsNoTracking().Where(x => !x.IsDeleted);

        return await query
            .Where(x =>
                x.IoTDeviceId == iotDeviceId &&
                x.Status == IotDeviceCommandConstants.Status.Pending &&
                (x.ExpiredAt == null || x.ExpiredAt > now))
            .OrderBy(x => x.CreatedDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Thực hiện truy vấn hoặc cập nhật dữ liệu ở tầng Infrastructure bằng EF Core.
    /// </summary>
    /// <param name="id">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotDeviceCommandAggregate?> GetDetailByIdAsync(int id)
    {
        return await BuildAggregateQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<DTResult<IotDeviceCommandAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = BuildAggregateQuery();
        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.CommandCode.Contains(keyword) ||
                x.CommandType.Contains(keyword) ||
                x.Status.Contains(keyword) ||
                x.DeviceCode.Contains(keyword) ||
                x.DeviceName.Contains(keyword) ||
                x.WarehouseCode.Contains(keyword) ||
                x.WarehouseName.Contains(keyword) ||
                (x.Payload != null && x.Payload.Contains(keyword)) ||
                (x.ResultMessage != null && x.ResultMessage.Contains(keyword)) ||
                (x.RequestedByFullName != null && x.RequestedByFullName.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "iotDeviceId":
                    case "IotDeviceId":
                        if (int.TryParse(search, out var deviceId))
                            query = query.Where(x => x.IotDeviceId == deviceId);
                        break;
                    case "warehouseId":
                    case "WarehouseId":
                        if (int.TryParse(search, out var warehouseId))
                            query = query.Where(x => x.WarehouseId == warehouseId);
                        break;
                    case "deviceCode":
                    case "DeviceCode":
                        query = query.Where(x => x.DeviceCode.Contains(search));
                        break;
                    case "commandCode":
                    case "CommandCode":
                        query = query.Where(x => x.CommandCode.Contains(search));
                        break;
                    case "commandType":
                    case "CommandType":
                        query = query.Where(x => x.CommandType == search.ToUpper());
                        break;
                    case "status":
                    case "Status":
                        query = query.Where(x => x.Status == search.ToUpper());
                        break;
                    case "createdDate":
                    case "CreatedDate":
                        if (DateTime.TryParse(search, out var createdDate))
                            query = query.Where(x => x.CreatedDate.Date == createdDate.Date);
                        break;
                }
            }
        }

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        return new DTResult<IotDeviceCommandAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };
    }

    private IQueryable<IotDeviceCommandAggregate> BuildAggregateQuery()
    {
        return from command in _context.IotDeviceCommands.AsNoTracking().Where(x => !x.IsDeleted)
               join device in _context.IotDevices.AsNoTracking().Where(x => !x.IsDeleted)
                    on command.IoTDeviceId equals device.Id
               join warehouse in _context.Warehouses.AsNoTracking().Where(x => !x.IsDeleted)
                    on device.WarehouseId equals warehouse.Id
               join requestedBy in _context.Users.AsNoTracking().Where(x => !x.IsDeleted)
                    on command.RequestedByUserId equals requestedBy.Id into requestedByGroup
               from requestedBy in requestedByGroup.DefaultIfEmpty()
               select new IotDeviceCommandAggregate
               {
                   Id = command.Id,
                   IotDeviceId = command.IoTDeviceId,
                   DeviceCode = device.DeviceCode,
                   DeviceName = device.DeviceName,
                   WarehouseId = device.WarehouseId,
                   WarehouseCode = warehouse.Code,
                   WarehouseName = warehouse.Name,
                   CommandCode = command.CommandCode,
                   CommandType = command.CommandType,
                   Payload = command.Payload,
                   Status = command.Status,
                   RequestedByUserId = command.RequestedByUserId,
                   RequestedByFullName = requestedBy == null ? null : (requestedBy.FirstName + " " + requestedBy.LastName),
                   RequestedAt = command.RequestedAt,
                   PickedUpAt = command.PickedUpAt,
                   ExecutedAt = command.ExecutedAt,
                   ExpiredAt = command.ExpiredAt,
                   ResultMessage = command.ResultMessage,
                   RetryCount = command.RetryCount,
                   CreatedDate = command.CreatedDate
               };
    }

    private static string NormalizeOrderColumn(string? column)
    {
        return column switch
        {
            "iotDeviceId" => "IotDeviceId",
            "deviceCode" => "DeviceCode",
            "deviceName" => "DeviceName",
            "warehouseId" => "WarehouseId",
            "warehouseCode" => "WarehouseCode",
            "warehouseName" => "WarehouseName",
            "commandCode" => "CommandCode",
            "commandType" => "CommandType",
            "status" => "Status",
            "requestedAt" => "RequestedAt",
            "pickedUpAt" => "PickedUpAt",
            "executedAt" => "ExecutedAt",
            "expiredAt" => "ExpiredAt",
            "retryCount" => "RetryCount",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}
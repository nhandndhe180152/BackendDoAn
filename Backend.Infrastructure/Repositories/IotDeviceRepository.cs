using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class IotDeviceRepository : RepositoryBase<IotDevice, int>, IIotDeviceRepository
{
    private readonly BackendContext _context;

    public IotDeviceRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy dữ liệu theo điều kiện nghiệp vụ cụ thể thay vì chỉ theo id.
    /// </summary>
    /// <param name="deviceCode">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="trackChanges">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotDevice?> GetByDeviceCodeAsync(string deviceCode, bool trackChanges = false)
    {
        var query = trackChanges
            ? _context.IotDevices.Where(x => !x.IsDeleted)
            : _context.IotDevices.AsNoTracking().Where(x => !x.IsDeleted);

        return await query.FirstOrDefaultAsync(x => x.DeviceCode == deviceCode);
    }

    public async Task<IotDeviceAggregate?> GetDetailByIdAsync(int id)
    {
        return await BuildAggregateQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<DTResult<IotDeviceAggregate>> GetPagedAsync(DTParameter parameters)
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
                x.DeviceName.Contains(keyword) ||
                x.DeviceCode.Contains(keyword) ||
                x.DeviceType.Contains(keyword) ||
                x.WarehouseName.Contains(keyword) ||
                x.WarehouseCode.Contains(keyword) ||
                (x.Location != null && x.Location.Contains(keyword)) ||
                (x.MqttTopic != null && x.MqttTopic.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "warehouseId":
                    case "WarehouseId":
                        if (int.TryParse(search, out var warehouseId))
                            query = query.Where(x => x.WarehouseId == warehouseId);
                        break;

                    case "deviceType":
                    case "DeviceType":
                        query = query.Where(x => x.DeviceType.Contains(search));
                        break;

                    case "isOnline":
                    case "IsOnline":
                        if (bool.TryParse(search, out var isOnline))
                            query = query.Where(x => x.IsOnline == isOnline);
                        break;

                    case "isActive":
                    case "IsActive":
                        if (bool.TryParse(search, out var isActive))
                            query = query.Where(x => x.IsActive == isActive);
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

        return new DTResult<IotDeviceAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private IQueryable<IotDeviceAggregate> BuildAggregateQuery()
    {
        return _context.IotDevices
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new IotDeviceAggregate
            {
                Id = x.Id,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                WarehouseCode = x.Warehouse.Code,
                DeviceName = x.DeviceName,
                DeviceCode = x.DeviceCode,
                DeviceType = x.DeviceType,
                Location = x.Location,
                MqttTopic = x.MqttTopic,
                LastHeartbeat = x.LastHeartbeat,
                IsOnline = x.IsOnline,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
            });
    }

    private static string NormalizeOrderColumn(string? columnName)
    {
        return columnName switch
        {
            "id" => "Id",
            "warehouseId" => "WarehouseId",
            "warehouseName" => "WarehouseName",
            "warehouseCode" => "WarehouseCode",
            "deviceName" => "DeviceName",
            "deviceCode" => "DeviceCode",
            "deviceType" => "DeviceType",
            "location" => "Location",
            "mqttTopic" => "MqttTopic",
            "lastHeartbeat" => "LastHeartbeat",
            "isOnline" => "IsOnline",
            "isActive" => "IsActive",
            "createdDate" => "CreatedDate",
            "lastModifiedDate" => "LastModifiedDate",
            _ => "Id"
        };
    }

    /// <summary>
    /// Thực hiện truy vấn hoặc cập nhật dữ liệu ở tầng Infrastructure bằng EF Core.
    /// </summary>
    /// <param name="deviceCode">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<IotDevice?> GetActiveByDeviceCodeAsync(string deviceCode)
    {
        return await _context.IotDevices
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.DeviceCode == deviceCode);
    }
}

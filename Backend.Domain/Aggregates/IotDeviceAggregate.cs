using System;

namespace Backend.Domain.Aggregates;

public class IotDeviceAggregate
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? MqttTopic { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

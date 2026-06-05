using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class IotDeviceCommandDetailDto
{
    public int Id { get; set; }
    public int IotDeviceId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string CommandCode { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? RequestedByUserId { get; set; }
    public string? RequestedByFullName { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string? ResultMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

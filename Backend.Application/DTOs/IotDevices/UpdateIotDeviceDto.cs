using System;

namespace Backend.Application.DTOs.IotDevices;

public class UpdateIotDeviceDto
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceCode { get; set; } = string.Empty;

    public string DeviceType { get; set; } = "SCALE";

    public string? Location { get; set; }

    public string? MqttTopic { get; set; }

    public bool IsActive { get; set; } = true;

    public int? UpdatedBy { get; set; }
}

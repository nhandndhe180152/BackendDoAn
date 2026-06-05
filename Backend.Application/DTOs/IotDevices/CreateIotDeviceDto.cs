using System;

namespace Backend.Application.DTOs.IotDevices;

public class CreateIotDeviceDto
{
    public int WarehouseId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceCode { get; set; } = string.Empty;

    public string DeviceType { get; set; } = "SCALE";

    public string? Location { get; set; }

    public string? MqttTopic { get; set; }

    /// <summary>
    /// Nếu không truyền lên, backend sẽ tự sinh Device Key.
    /// Device Key chỉ trả về 1 lần khi tạo hoặc regenerate.
    /// </summary>
    public string? ApiKey { get; set; }

    public bool IsActive { get; set; } = true;

    public int? CreatedBy { get; set; }
}

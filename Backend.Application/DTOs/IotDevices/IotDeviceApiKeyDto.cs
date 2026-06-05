using System;

namespace Backend.Application.DTOs.IotDevices;

public class IotDeviceApiKeyDto
{
    public int Id { get; set; }

    public string DeviceCode { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string HeaderName { get; set; } = "X-Device-Key";
}

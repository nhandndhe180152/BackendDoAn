using System;

namespace Backend.Application.DTOs.UserDevices;

public class CreateUserDeviceDto
{
    public int? UserId { get; set; }
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string DeviceToken { get; set; } = null!;
    public string? UserAgent { get; set; }
}

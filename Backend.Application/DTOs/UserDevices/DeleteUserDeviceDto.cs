using System;

namespace Backend.Application.DTOs.UserDevices;

public class DeleteUserDeviceDto
{
    public int? UserId { get; set; }
    public string DeviceToken { get; set; } = null!;
}

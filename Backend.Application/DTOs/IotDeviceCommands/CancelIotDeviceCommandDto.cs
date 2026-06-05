using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class CancelIotDeviceCommandDto
{
    public string? Reason { get; set; }
    public int? UpdatedBy { get; set; }
}

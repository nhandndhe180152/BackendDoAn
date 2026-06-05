using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class CompleteIotDeviceCommandDto
{
    public bool Success { get; set; }
    public string? ResultMessage { get; set; }
}

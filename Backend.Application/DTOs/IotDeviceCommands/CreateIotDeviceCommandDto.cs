using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class CreateIotDeviceCommandDto
{
    public int IotDeviceId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public int? CreatedBy { get; set; }
}

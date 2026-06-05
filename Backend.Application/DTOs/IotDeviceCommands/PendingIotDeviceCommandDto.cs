using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class PendingIotDeviceCommandDto
{
    public int CommandId { get; set; }
    public string CommandCode { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime ServerTime { get; set; }
}

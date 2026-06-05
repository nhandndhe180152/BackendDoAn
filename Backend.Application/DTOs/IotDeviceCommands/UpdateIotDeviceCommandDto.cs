using System;

namespace Backend.Application.DTOs.IotDeviceCommands;

public class UpdateIotDeviceCommandDto
{
    public int Id { get; set; }
    public int IotDeviceId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiredAt { get; set; }
    public string? ResultMessage { get; set; }
    public int? UpdatedBy { get; set; }
}

using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class IotDeviceCommand : EntityAuditBase<int>
{
    public int IoTDeviceId { get; set; }
    public string CommandCode { get; set; } = null!;
    public string CommandType { get; set; } = null!;
    public string? Payload { get; set; }
    public string Status { get; set; } = null!;
    public int? RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string? ResultMessage { get; set; }
    public int RetryCount { get; set; }

    public virtual IotDevice IotDevice { get; set; } = null!;
    public virtual User? RequestedByUser { get; set; }
}

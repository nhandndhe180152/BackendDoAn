using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class IotDevice : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public string ApiKeyHash { get; set; } = null!;
    public string DeviceName { get; set; } = null!;
    public string DeviceCode { get; set; } = null!;
    public string DeviceType { get; set; } = null!;
    public string? Location { get; set; }
    public string? MqttTopic { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public bool IsActive { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ICollection<IotDeviceCommand> IotDeviceCommands { get; set; } = new List<IotDeviceCommand>();
    public virtual ICollection<IotWeightLog> IotWeightLogs { get; set; } = new List<IotWeightLog>();
}

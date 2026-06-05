using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class IotWeightLog : EntityAuditBase<int>
{
    public int IoTDeviceId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public int? ReferenceItemId { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? RawValue { get; set; }
    public string Unit { get; set; } = "kg";
    public bool IsStable { get; set; }
    public DateTime MeasuredAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsConfirmed { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? RequestIpAddress { get; set; }

    public virtual IotDevice IotDevice { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
}

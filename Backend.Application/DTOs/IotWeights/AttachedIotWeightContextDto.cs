using System;

namespace Backend.Application.DTOs.IotWeights;

public class AttachedIotWeightContextDto
{
    public int LogId { get; set; }

    public int IotDeviceId { get; set; }

    public string? DeviceCode { get; set; }

    public decimal WeightKg { get; set; }

    public decimal? RawValue { get; set; }

    public string Unit { get; set; } = "kg";

    public bool IsStable { get; set; }

    public bool IsConfirmed { get; set; }

    public int? ProductVariantId { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? ReferenceItemId { get; set; }

    public DateTime MeasuredAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public decimal? ReferenceItemActualWeightKg { get; set; }
}

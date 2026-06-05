using System;

namespace Backend.Application.DTOs.IotWeights;

public class IotWeightReceivedDto
{
    public int LogId { get; set; }

    public string DeviceCode { get; set; } = null!;

    public decimal WeightKg { get; set; }

    public decimal? RawValue { get; set; }

    public string Unit { get; set; } = "kg";

    public bool IsStable { get; set; }

    public DateTime MeasuredAt { get; set; }

    public DateTime ReceivedAt { get; set; }
}

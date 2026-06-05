using System;

namespace Backend.Application.DTOs.IotWeights;

public class ReceiveIotWeightDto
{
    public string DeviceCode { get; set; } = null!;

    // ESP32 có thể gửi "weight" hoặc "weightKg".
    public decimal? Weight { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? RawValue { get; set; }

    public string? Unit { get; set; } = "kg";

    public bool IsStable { get; set; }

    public int? ProductVariantId { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? ReferenceItemId { get; set; }

    public DateTime? MeasuredAt { get; set; }
}

using System;
using Backend.Application.DTOs.IotWeights;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class IotWeightMapping
{
    public static IotWeightLog ToEntity(
        this ReceiveIotWeightDto dto,
        IotDevice device,
        decimal normalizedWeightKg,
        string? requestIpAddress)
    {
        var now = DateTime.Now;

        return new IotWeightLog
        {
            IoTDeviceId = device.Id,
            ProductVariantId = dto.ProductVariantId,
            ReferenceType = string.IsNullOrWhiteSpace(dto.ReferenceType) ? null : dto.ReferenceType.Trim(),
            ReferenceId = dto.ReferenceId,
            ReferenceItemId = dto.ReferenceItemId,
            WeightKg = Math.Round(normalizedWeightKg, 3),
            RawValue = dto.RawValue,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "kg" : dto.Unit.Trim(),
            IsStable = dto.IsStable,
            MeasuredAt = dto.MeasuredAt ?? now,
            ReceivedAt = now,
            IsConfirmed = false,
            RequestIpAddress = requestIpAddress,
            CreatedDate = now
        };
    }

    public static IotWeightReceivedDto ToReceivedDto(this IotWeightLog entity, IotDevice device)
    {
        return new IotWeightReceivedDto
        {
            LogId = entity.Id,
            DeviceCode = device.DeviceCode,
            WeightKg = entity.WeightKg,
            RawValue = entity.RawValue,
            Unit = entity.Unit,
            IsStable = entity.IsStable,
            MeasuredAt = entity.MeasuredAt,
            ReceivedAt = entity.ReceivedAt
        };
    }

    public static LatestIotWeightDto ToLatestDto(this IotWeightLog entity, IotDevice device)
    {
        return new LatestIotWeightDto
        {
            LogId = entity.Id,
            IotDeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            DeviceName = device.DeviceName,
            WeightKg = entity.WeightKg,
            RawValue = entity.RawValue,
            Unit = entity.Unit,
            IsStable = entity.IsStable,
            IsOnline = device.IsOnline,
            LastHeartbeat = device.LastHeartbeat,
            MeasuredAt = entity.MeasuredAt,
            ReceivedAt = entity.ReceivedAt
        };
    }

    public static AttachedIotWeightContextDto ToAttachedContextDto(
        this IotWeightLog entity,
        decimal? referenceItemActualWeightKg = null)
    {
        return new AttachedIotWeightContextDto
        {
            LogId = entity.Id,
            IotDeviceId = entity.IoTDeviceId,
            DeviceCode = entity.IotDevice?.DeviceCode,
            WeightKg = entity.WeightKg,
            RawValue = entity.RawValue,
            Unit = entity.Unit,
            IsStable = entity.IsStable,
            IsConfirmed = entity.IsConfirmed,
            ProductVariantId = entity.ProductVariantId,
            ReferenceType = entity.ReferenceType,
            ReferenceId = entity.ReferenceId,
            ReferenceItemId = entity.ReferenceItemId,
            MeasuredAt = entity.MeasuredAt,
            ReceivedAt = entity.ReceivedAt,
            ConfirmedBy = entity.ConfirmedBy,
            ConfirmedAt = entity.ConfirmedAt,
            ReferenceItemActualWeightKg = referenceItemActualWeightKg
        };
    }
}

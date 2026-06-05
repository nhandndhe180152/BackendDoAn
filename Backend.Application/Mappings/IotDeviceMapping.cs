using System;
using Backend.Application.DTOs.IotDevices;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class IotDeviceMapping
{
    public static IotDevice ToEntity(this CreateIotDeviceDto dto, string plainApiKey)
    {
        var now = DateTime.Now;

        return new IotDevice
        {
            WarehouseId = dto.WarehouseId,
            DeviceName = dto.DeviceName.Trim(),
            DeviceCode = dto.DeviceCode.Trim().ToUpperInvariant(),
            DeviceType = string.IsNullOrWhiteSpace(dto.DeviceType) ? "SCALE" : dto.DeviceType.Trim().ToUpperInvariant(),
            Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
            MqttTopic = string.IsNullOrWhiteSpace(dto.MqttTopic) ? null : dto.MqttTopic.Trim(),
            ApiKeyHash = DeviceKeyHelper.HashKey(plainApiKey.Trim()), // Hashing is done in the service layer, store the trimmed plain key here temporarily
            IsOnline = false,
            IsActive = dto.IsActive,
            IsDeleted = false,
            CreatedBy = dto.CreatedBy,
            CreatedDate = now
        };
    }

    public static IotDevice ToEntity(this UpdateIotDeviceDto dto, IotDevice entity)
    {
        entity.WarehouseId = dto.WarehouseId;
        entity.DeviceName = dto.DeviceName.Trim();
        entity.DeviceCode = dto.DeviceCode.Trim().ToUpperInvariant();
        entity.DeviceType = string.IsNullOrWhiteSpace(dto.DeviceType) ? "SCALE" : dto.DeviceType.Trim().ToUpperInvariant();
        entity.Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        entity.MqttTopic = string.IsNullOrWhiteSpace(dto.MqttTopic) ? null : dto.MqttTopic.Trim();
        entity.IsActive = dto.IsActive;
        entity.UpdatedBy = dto.UpdatedBy;
        entity.LastModifiedDate = DateTime.Now;

        return entity;
    }

    public static IotDeviceDetailDto ToDto(this IotDeviceAggregate entity)
    {
        return new IotDeviceDetailDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.WarehouseName,
            WarehouseCode = entity.WarehouseCode,
            DeviceName = entity.DeviceName,
            DeviceCode = entity.DeviceCode,
            DeviceType = entity.DeviceType,
            Location = entity.Location,
            MqttTopic = entity.MqttTopic,
            LastHeartbeat = entity.LastHeartbeat,
            IsOnline = entity.IsOnline,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate
        };
    }
}

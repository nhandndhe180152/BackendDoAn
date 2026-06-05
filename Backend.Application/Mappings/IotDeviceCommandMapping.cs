using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class IotDeviceCommandMapping
{
    public static IotDeviceCommand ToEntity(this CreateIotDeviceCommandDto dto, string commandCode)
    {
        var now = DateTime.Now;

        return new IotDeviceCommand
        {
            IoTDeviceId = dto.IotDeviceId,
            CommandCode = commandCode,
            CommandType = dto.CommandType.Trim().ToUpperInvariant(),
            Payload = string.IsNullOrWhiteSpace(dto.Payload) ? null : dto.Payload.Trim(),
            Status = IotDeviceCommandConstants.Status.Pending,
            RequestedByUserId = dto.CreatedBy,
            RequestedAt = now,
            ExpiredAt = dto.ExpiredAt ?? now.AddMinutes(2),
            RetryCount = 0,
            IsDeleted = false,
            CreatedBy = dto.CreatedBy,
            CreatedDate = now
        };
    }

    public static IotDeviceCommand ToEntity(this UpdateIotDeviceCommandDto dto, IotDeviceCommand entity)
    {
        entity.IoTDeviceId = dto.IotDeviceId;
        entity.CommandType = dto.CommandType.Trim().ToUpperInvariant();
        entity.Payload = string.IsNullOrWhiteSpace(dto.Payload) ? null : dto.Payload.Trim();
        entity.Status = dto.Status.Trim().ToUpperInvariant();
        entity.ExpiredAt = dto.ExpiredAt;
        entity.ResultMessage = string.IsNullOrWhiteSpace(dto.ResultMessage) ? null : dto.ResultMessage.Trim();
        entity.UpdatedBy = dto.UpdatedBy;
        entity.LastModifiedDate = DateTime.Now;

        return entity;
    }

    public static IotDeviceCommandDetailDto ToDto(this IotDeviceCommandAggregate entity)
    {
        return new IotDeviceCommandDetailDto
        {
            Id = entity.Id,
            IotDeviceId = entity.IotDeviceId,
            DeviceCode = entity.DeviceCode,
            DeviceName = entity.DeviceName,
            WarehouseId = entity.WarehouseId,
            WarehouseCode = entity.WarehouseCode,
            WarehouseName = entity.WarehouseName,
            CommandCode = entity.CommandCode,
            CommandType = entity.CommandType,
            Payload = entity.Payload,
            Status = entity.Status,
            RequestedByUserId = entity.RequestedByUserId,
            RequestedByFullName = entity.RequestedByFullName,
            RequestedAt = entity.RequestedAt,
            PickedUpAt = entity.PickedUpAt,
            ExecutedAt = entity.ExecutedAt,
            ExpiredAt = entity.ExpiredAt,
            ResultMessage = entity.ResultMessage,
            RetryCount = entity.RetryCount,
            CreatedDate = entity.CreatedDate
        };
    }

    public static PendingIotDeviceCommandDto ToPendingDto(this IotDeviceCommand entity)
    {
        return new PendingIotDeviceCommandDto
        {
            CommandId = entity.Id,
            CommandCode = entity.CommandCode,
            CommandType = entity.CommandType,
            Payload = entity.Payload,
            RequestedAt = entity.RequestedAt,
            ExpiredAt = entity.ExpiredAt,
            ServerTime = DateTime.Now
        };
    }
}

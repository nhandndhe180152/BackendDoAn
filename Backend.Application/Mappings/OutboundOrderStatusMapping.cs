using System;
using Backend.Application.DTOs.OutboundOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class OutboundOrderStatusMapping
{
    public static OutboundOrderStatus ToEntity(this CreateOutboundOrderStatusDto dto)
    {
        return new OutboundOrderStatus
        {
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static OutboundOrderStatus ToEntity(this UpdateOutboundOrderStatusDto dto, OutboundOrderStatus existData)
    {
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}

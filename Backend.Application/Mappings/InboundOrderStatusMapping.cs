using System;
using Backend.Application.DTOs.InboundOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class InboundOrderStatusMapping
{
    public static InboundOrderStatus ToEntity(this CreateInboundOrderStatusDto dto)
    {
        return new InboundOrderStatus
        {
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static InboundOrderStatus ToEntity(this UpdateInboundOrderStatusDto dto, InboundOrderStatus existData)
    {
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}

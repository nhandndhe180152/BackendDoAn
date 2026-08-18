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
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static OutboundOrderStatus ToEntity(this UpdateOutboundOrderStatusDto dto, OutboundOrderStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}

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
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static InboundOrderStatus ToEntity(this UpdateInboundOrderStatusDto dto, InboundOrderStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
